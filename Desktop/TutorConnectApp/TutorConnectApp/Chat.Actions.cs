using System.ComponentModel;
using System.Diagnostics;

namespace TutorConnectApp
{
    // Műveletek: küldés (azonnali megjelenéssel), csatolmány, szerkesztés, visszavonás, profilok
    internal sealed partial class ChatPage
    {
        private const int MaxMessageLength = 2000;

        /// <summary>A küldésre váró (és a szerver válaszára váró) saját üzenetek, a küldés sorrendjében.</summary>
        private readonly Queue<MessageDto> outbox = new();
        private bool outboxRunning;
        private int pendingId; // az ideiglenes (negatív) azonosítók számlálója

        // ------------------------------------------------------------
        // Szöveges üzenet küldése
        // ------------------------------------------------------------

        /// <summary>
        /// Az üzenet azonnal megjelenik ("küldés…" jelzéssel), a szerverre a háttérben megy el, a sorrend megmarad.
        /// Hiba esetén az üzenet pirosan marad, és kattintásra újra próbálható.
        /// </summary>
        private async Task SendAsync()
        {
            var open = current;
            string text = composer.Value;
            if (open == null || text.Length == 0) return;
            if (text.Length > MaxMessageLength)
            {
                ShowStatus($"Az üzenet legfeljebb {MaxMessageLength} karakter lehet.", error: true);
                return;
            }

            ClearComposer(open.Id);

            var pending = new MessageDto
            {
                Id = --pendingId,
                ConversationId = open.Id,
                SenderId = me.Id,
                Content = text,
                CreatedAt = DateTime.UtcNow,
                Send = SendState.Sending
            };
            AppendMessage(pending, forceStick: true);
            outbox.Enqueue(pending);

            await ProcessOutboxAsync();
        }

        /// <summary>A beviteli mező ürítése küldés után (nem számít szerkesztésnek, ezért nem jelez "ír..."-t a másik félnek).</summary>
        private void ClearComposer(int conversationId)
        {
            restoringDraft = true;
            composer.SetValue("");
            restoringDraft = false;

            Drafts.Remove(conversationId);
            RefreshDraftBadge();
        }

        private async Task ProcessOutboxAsync()
        {
            if (outboxRunning) return;

            outboxRunning = true;
            try
            {
                while (outbox.Count > 0 && !IsDisposed)
                {
                    var message = outbox.Peek();
                    try
                    {
                        var saved = await ApiClient.PostAsync<MessageDto>($"/api/conversations/{message.ConversationId}/messages",
                            new Dictionary<string, object?> { ["content"] = message.Content });
                        outbox.Dequeue();
                        if (IsDisposed) return;

                        ConfirmSent(message, saved);
                    }
                    catch (ApiException ex)
                    {
                        if (IsDisposed) return;
                        FailOutbox(ex.Message);
                    }
                }
            }
            finally
            {
                outboxRunning = false;
            }
        }

        /// <summary>A szerver elfogadta az üzenetet: az ideiglenes buborék a valódira cserélődik.</summary>
        private void ConfirmSent(MessageDto pending, MessageDto saved)
        {
            saved.Send = SendState.Sent;
            ChatMessages.Add(saved);
            if (ChatState.Find(saved.ConversationId) is { } conversation)
                ChatState.Bump(conversation, saved.Content, saved.CreatedAt);
            ResetReceiptPolling();

            if (current?.Id != saved.ConversationId) return; // közben másik beszélgetésre váltottunk: a gyorsítótár már megkapta

            int index = messages.IndexOf(pending);
            bool known = messages.Any(m => m.Id == saved.Id);

            if (index >= 0 && known)
            {
                RemoveMessage(pending);
            }
            else if (index >= 0)
            {
                messages[index] = saved;
                ReplaceBubble(pending.Id, saved);
            }
            else if (!known)
            {
                AppendMessage(saved);
            }
        }

        /// <summary>A küldés nem sikerült: a sorban álló üzenetek mind hibásnak jelölődnek (a sorrend így nem borul fel).</summary>
        private void FailOutbox(string reason)
        {
            var failed = outbox.ToList();
            outbox.Clear();

            foreach (var message in failed)
            {
                message.Send = SendState.Failed;

                if (current?.Id == message.ConversationId && messages.Contains(message))
                {
                    ReplaceBubble(message.Id, message);
                }
                else
                {
                    // másik beszélgetésben történt: a szöveg a piszkozatba kerül, hogy ne vesszen el
                    string existing = DraftFor(message.ConversationId);
                    Drafts[message.ConversationId] = existing.Length == 0 ? message.Content : message.Content + "\n" + existing;
                }
            }

            RefreshDraftBadge();
            ShowStatus(reason, error: true);
        }

        private async Task RetryAsync(MessageDto message)
        {
            if (message.Send != SendState.Failed || outbox.Contains(message)) return;

            message.Send = SendState.Sending;
            ReplaceBubble(message.Id, message);
            outbox.Enqueue(message);
            await ProcessOutboxAsync();
        }

        /// <summary>A sikertelen üzenet elvetése: a szövege visszakerül a beviteli mezőbe, ha az üres.</summary>
        private void DiscardFailed(MessageDto message)
        {
            if (message.Send != SendState.Failed) return;

            RemoveMessage(message);
            if (composer.Value.Length == 0)
            {
                composer.SetValue(message.Content);
                composer.FocusInput();
            }
        }

        // ------------------------------------------------------------
        // Csatolmány
        // ------------------------------------------------------------

        /// <summary>A szerver által elfogadott csatolmány-kiterjesztések.</summary>
        private static readonly HashSet<string> SendableExtensions = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt" };

        private async Task AttachAsync()
        {
            if (current == null || busy) return;

            using var dialog = new OpenFileDialog
            {
                Title = "Fájl csatolása",
                Filter = "Támogatott fájlok|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt|Minden fájl|*.*"
            };
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

            await UploadFileAsync(dialog.FileName, confirm: false);
        }

        /// <summary>
        /// Fájl küldése a megnyitott beszélgetésbe. A behúzott vagy beillesztett fájlnál (confirm) előbb rákérdez,
        /// hogy véletlenül ne menjen el valami.
        /// </summary>
        private async Task UploadFileAsync(string filePath, bool confirm)
        {
            var open = current;
            if (open == null || busy) return;

            string extension = Path.GetExtension(filePath);
            if (!SendableExtensions.Contains(extension))
            {
                ShowStatus("Ezt a fájltípust nem lehet elküldeni (kép, pdf, Office-fájl és txt támogatott).", error: true);
                return;
            }

            if (confirm)
            {
                long size = 0;
                try { size = new FileInfo(filePath).Length; }
                catch (IOException) { }

                string text = $"Elküldöd ezt a fájlt?\n\n{Path.GetFileName(filePath)}" + (size > 0 ? $"  ({Ui.FormatBytes(size)})" : "");
                if (MessageBox.Show(FindForm(), text, "Fájl küldése", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            busy = true;
            ShowStatus("Feltöltés...", error: false, autoHide: false);
            try
            {
                string caption = composer.Value;
                var message = await ApiClient.UploadAsync<MessageDto>($"/api/conversations/{open.Id}/attachments", filePath, caption);
                if (IsDisposed) return;

                ChatMessages.Add(message);
                if (current?.Id == open.Id)
                {
                    ClearComposer(open.Id);
                    AppendMessage(message, forceStick: true);
                }
                ChatState.Bump(open, message.Content, message.CreatedAt);
                ShowStatus("", error: false);
            }
            catch (ApiException ex)
            {
                ShowStatus(ex.Message, error: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowStatus("A fájl nem olvasható: " + ex.Message, error: true);
            }
            finally
            {
                busy = false;
            }
        }

        private static bool IsImage(MessageDto message) =>
            (message.AttachmentType ?? "").StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(Path.GetExtension(message.AttachmentName ?? "").ToLowerInvariant());

        /// <summary>A képek a beépített képnézegetőben nyílnak meg (kattintásra), a többi fájl mentés / megnyitás szerint.</summary>
        private async Task OpenAttachmentAsync(MessageDto message, bool open)
        {
            if (IsImage(message)) await ShowImageAsync(message);
            else await DownloadAsync(message, open);
        }

        private async Task ShowImageAsync(MessageDto message)
        {
            string name = SafeFileName(message.AttachmentName);
            try
            {
                ShowStatus("Kép betöltése...", error: false, autoHide: false);
                string folder = Path.Combine(Path.GetTempPath(), "TutorConnect", message.Id.ToString());
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, name);

                await ApiClient.DownloadAsync($"/api/messages/{message.Id}/attachment", path);
                if (IsDisposed) return;
                ShowStatus("", error: false);

                using var viewer = new ImageViewerForm(path, name);
                viewer.ShowDialog(FindForm());
            }
            catch (ApiException ex)
            {
                ShowStatus(ex.Message, error: true);
            }
            catch (ArgumentException)
            {
                // nem olvasható képként: a megszokott módon mentjük / megnyitjuk
                ShowStatus("", error: false);
                await DownloadAsync(message, open: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OutOfMemoryException)
            {
                ShowStatus("A kép megnyitása nem sikerült: " + ex.Message, error: true);
            }
        }

        /// <summary>Kiterjesztések, amelyek megnyitása nem futtathat kódot (makró, szkript) – ezeket kérdés nélkül megnyitjuk.</summary>
        private static readonly HashSet<string> SafeToOpen = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".txt" };

        /// <summary>Mentés másként (open = false), vagy ideiglenes mappába töltés és megnyitás a "Megnyitás" gombra.</summary>
        private async Task DownloadAsync(MessageDto message, bool open)
        {
            string name = SafeFileName(message.AttachmentName);
            string path;

            if (open)
            {
                // az Office-fájlok makrót tartalmazhatnak: megnyitás előtt rákérdezünk
                if (!SafeToOpen.Contains(Path.GetExtension(name)) &&
                    MessageBox.Show(FindForm(),
                        "Ezt a fájlt a számítógépeden lévő program nyitja meg. Csak akkor folytasd, ha ismered és megbízol a feladóban.\n\nMegnyitod?",
                        "Fájl megnyitása", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                string folder = Path.Combine(Path.GetTempPath(), "TutorConnect", message.Id.ToString());
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, name);
            }
            else
            {
                using var dialog = new SaveFileDialog { FileName = name, Title = "Csatolmány mentése" };
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                path = dialog.FileName;
            }

            try
            {
                ShowStatus("Letöltés...", error: false, autoHide: false);
                await ApiClient.DownloadAsync($"/api/messages/{message.Id}/attachment", path);
                ShowStatus(open ? "" : "A fájl elmentve.", error: false);

                if (open)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    }
                    catch (Win32Exception)
                    {
                        ShowStatus("Nincs program, ami meg tudná nyitni ezt a fájlt. Használd a mentést.", error: true);
                    }
                }
            }
            catch (ApiException ex)
            {
                ShowStatus(ex.Message, error: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowStatus("A fájl mentése nem sikerült: " + ex.Message, error: true);
            }
        }

        /// <summary>A szervertől kapott fájlnévből eltávolítja az útvonalat és a tiltott karaktereket.</summary>
        private static string SafeFileName(string? raw)
        {
            string name = Path.GetFileName((raw ?? "").Replace('/', '\\'));
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            name = name.Trim().TrimEnd('.');
            return name.Length == 0 ? "fajl" : name;
        }

        // ------------------------------------------------------------
        // Új beszélgetés és profilok
        // ------------------------------------------------------------

        private async Task NewConversationAsync()
        {
            try
            {
                var tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                if (IsDisposed) return;

                using var dialog = new NewConversationDialog(tutors, ChatState.Conversations.Select(c => c.TutorId));
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

                await ChatState.LoadAsync();
                if (!IsDisposed && ChatState.Find(dialog.ConversationId) is { } created)
                    await SelectAsync(created);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Nem sikerült megnyitni a beszélgetést", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Diáknál az oktató profilja (foglalással, naptárral), oktatónál a diák profilja.</summary>
        private async Task OpenProfileAsync()
        {
            var conversation = current;
            if (conversation == null) return;

            try
            {
                if (me.Role == "STUDENT")
                {
                    var tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                    if (IsDisposed) return;

                    var tutor = tutors.FirstOrDefault(t => t.Id == conversation.TutorId);
                    if (tutor == null)
                    {
                        ShowStatus("Az oktató profilja nem érhető el.", error: true);
                        return;
                    }
                    await TutorActions.ShowProfileAsync(FindForm(), tutor, Navigate);
                }
                else
                {
                    var student = await ApiClient.GetAsync<StudentProfileDto>($"/api/students/{conversation.StudentId}/profile");
                    if (IsDisposed) return;

                    using var dialog = new StudentProfileDialog(student);
                    dialog.ShowDialog(FindForm());
                }
            }
            catch (ApiException ex)
            {
                ShowStatus(ex.Message, error: true);
            }
        }

        // ------------------------------------------------------------
        // Szerkesztés, visszavonás, olvasottság
        // ------------------------------------------------------------

        /// <summary>
        /// A saját szerkesztésről a szerver nekünk is küld eseményt; ezt nem kell újratöltéssel kezelni,
        /// mert a módosítást már helyben alkalmaztuk. Üzenetazonosító → a szerkesztés időpontja.
        /// </summary>
        private readonly Dictionary<int, long> selfEdits = new();

        private async Task EditAsync(MessageDto message)
        {
            using var dialog = new EditMessageDialog(message.Content);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

            try
            {
                // az esemény a válasz előtt is megérkezhet, ezért a kérés előtt jegyezzük fel
                selfEdits[message.Id] = Environment.TickCount64;
                await ApiClient.PatchAsync($"/api/messages/{message.Id}", new Dictionary<string, object?> { ["content"] = dialog.NewText });
                if (IsDisposed) return;

                // a módosítást helyben alkalmazzuk, nincs szükség az üzenetek újratöltésére
                var local = messages.FirstOrDefault(m => m.Id == message.Id);
                if (local != null)
                {
                    local.Content = dialog.NewText;
                    local.EditedAt = DateTime.UtcNow;
                    ReplaceBubble(local);
                    RefreshPreviewIfLast(local);
                }
            }
            catch (ApiException ex)
            {
                selfEdits.Remove(message.Id);
                ShowStatus(ex.Message, error: true);
            }
        }

        private async Task RetractAsync(MessageDto message)
        {
            if (MessageBox.Show(FindForm(), "Biztosan visszavonod ezt az üzenetet? A másik fél sem fogja látni a tartalmát.",
                    "Üzenet visszavonása", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                await ApiClient.DeleteAsync($"/api/messages/{message.Id}");
                if (IsDisposed) return;
                ApplyRetracted(message.Id);
            }
            catch (ApiException ex)
            {
                ShowStatus(ex.Message, error: true);
            }
        }

        /// <summary>Visszavont üzenet helyi jelölése (saját visszavonás után, illetve a másik fél eseményére).</summary>
        private void ApplyRetracted(int messageId)
        {
            var local = messages.FirstOrDefault(m => m.Id == messageId);
            if (local == null || local.IsRetracted) return;

            local.DeletedAt = DateTime.UtcNow;
            ReplaceBubble(local);
            RefreshPreviewIfLast(local);
        }

        private async Task MarkReadAsync()
        {
            var open = current;
            if (open == null) return;

            // a másiktól érkezett üzenetek innentől olvasottak (a gyorsítótárban is, így újranyitáskor nincs "Új üzenetek" jelző)
            foreach (var message in messages.Where(m => m.SenderId != me.Id && !m.IsRead))
                message.IsReadValue = 1;

            if (open.UnreadCount == 0) return;

            ChatState.MarkRead(open); // a lista és a navbár jelvénye azonnal frissül
            try
            {
                await ApiClient.PostAsync($"/api/conversations/{open.Id}/read");
            }
            catch (ApiException)
            {
                // nem kritikus: a következő betöltéskor a szerver állapota érvényesül
            }
        }
    }
}

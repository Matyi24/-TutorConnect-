using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>Az alkalmazás szintű "frissítsd a jelvényeket" jelzés (olvasatlan üzenetek, függő foglalások).</summary>
    internal static class AppEvents
    {
        public static event Action? BadgesStale;

        public static void RaiseBadgesStale() => BadgesStale?.Invoke();
    }

    // ================================================================
    // CHAT: kétpaneles felület (beszélgetések + üzenetek), valós idejű frissítéssel
    // ================================================================
    internal sealed class ChatPage : PageBase
    {
        /// <summary>Az Oktatók oldal ezzel nyitja meg a chatet egy adott beszélgetésnél.</summary>
        public static int? PendingConversationId;

        private readonly UserInfo me;
        private readonly Panel listPanel;
        private readonly Panel threadPanel;
        private readonly SmoothScrollPanel listScroll;
        private readonly SmoothScrollPanel messageScroll;
        private readonly Panel threadHeader;
        private readonly Panel composerBar;
        private readonly Label placeholder;
        private readonly Label listMessage;
        private readonly Label headerName;
        private readonly Label headerStatus;
        private readonly HoverButton backButton;
        private readonly HoverButton attachButton;
        private readonly HoverButton sendButton;
        private readonly ComposerBox composer;
        private readonly System.Windows.Forms.Timer statusTimer = new() { Interval = 3000 };

        private List<ConversationDto> conversations = new();
        private List<MessageDto> messages = new();
        private ConversationDto? current;
        private bool showThread;
        private bool busy;
        private long lastTypingSent;
        private int lastMessageWidth = -1;

        public ChatPage(UserInfo me) : base("Chat", "", scrollable: false)
        {
            this.me = me;

            // ---------- bal oldal: beszélgetések ----------
            listPanel = new Panel { BackColor = Theme.CardBg, Dock = DockStyle.Left, Width = Ui.S(340) };
            listPanel.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.CardBorder);
                e.Graphics.DrawLine(pen, listPanel.Width - 1, 0, listPanel.Width - 1, listPanel.Height);
            };

            var listHeader = new Panel { Dock = DockStyle.Top, Height = Ui.S(72), BackColor = Theme.CardBg };
            var listTitle = Ui.Lbl("Üzenetek", 17F, Theme.Text, bold: true);
            listTitle.Location = Ui.Pt(24, 20);
            listHeader.Controls.Add(listTitle);

            listScroll = new SmoothScrollPanel { Dock = DockStyle.Fill, BackColor = Theme.CardBg };
            listScroll.Content.Padding = Ui.Pad(8, 0, 0, 12);
            listScroll.Content.ClientSizeChanged += (_, _) => FitList();

            listMessage = Ui.Lbl("", 10F, Theme.Muted);
            listMessage.MaximumSize = new Size(Ui.S(280), 0);
            listMessage.Margin = Ui.Pad(16, 8, 8, 8);

            listPanel.Controls.Add(listScroll);
            listPanel.Controls.Add(listHeader);

            // ---------- jobb oldal: beszélgetés ----------
            threadPanel = new Panel { BackColor = Theme.PageBg, Dock = DockStyle.Fill };

            placeholder = new Label
            {
                Text = "Válassz egy beszélgetést a bal oldalon.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Theme.Muted,
                BackColor = Theme.PageBg,
                Font = Ui.MakeFont(11F)
            };

            messageScroll = new SmoothScrollPanel { Dock = DockStyle.Fill, BackColor = Theme.PageBg, Visible = false };
            messageScroll.Content.Padding = Ui.Pad(24, 16, 10, 16);
            messageScroll.Content.ClientSizeChanged += (_, _) =>
            {
                // csak szélességváltozásnál kell újraszámolni (a magasság az új üzenetektől változik)
                int width = messageScroll.Content.ClientSize.Width;
                if (width == lastMessageWidth) return;
                lastMessageWidth = width;
                FitMessages();
            };

            threadHeader = new Panel { Dock = DockStyle.Top, Height = Ui.S(72), BackColor = Theme.CardBg, Visible = false };
            threadHeader.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.CardBorder);
                e.Graphics.DrawLine(pen, 0, threadHeader.Height - 1, threadHeader.Width, threadHeader.Height - 1);
            };
            backButton = Ui.Btn("", Theme.CardBg, Theme.Text, (_, _) => BackToList(), outline: true);
            backButton.Font = Ui.IconFont(12F);
            backButton.Text = "";
            backButton.AutoSize = false;
            backButton.Size = Ui.Sz(44, 44);
            backButton.Location = Ui.Pt(16, 14);
            backButton.Visible = false;
            headerName = Ui.Lbl("", 13F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 0));
            headerStatus = Ui.Lbl("", 9.5F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 0));
            threadHeader.Controls.Add(backButton);
            threadHeader.Controls.Add(headerName);
            threadHeader.Controls.Add(headerStatus);
            threadHeader.Resize += (_, _) => LayoutHeader();

            composerBar = new Panel { Dock = DockStyle.Bottom, Height = Ui.S(80), BackColor = Theme.CardBg, Visible = false };
            composerBar.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.CardBorder);
                e.Graphics.DrawLine(pen, 0, 0, composerBar.Width, 0);
            };
            attachButton = Ui.Btn("", Theme.CardBg, Theme.Text, async (_, _) => await AttachAsync(), outline: true);
            attachButton.Font = Ui.IconFont(14F);
            attachButton.Text = "";
            attachButton.AutoSize = false;
            attachButton.Size = Ui.Sz(50, 50);
            composer = new ComposerBox("Írj üzenetet...");
            composer.SendRequested += async (_, _) => await SendAsync();
            composer.TextEdited += (_, _) => NotifyTyping();
            sendButton = Ui.PrimaryBtn("Küldés", async (_, _) => await SendAsync());
            sendButton.AutoSize = false;
            sendButton.Size = Ui.Sz(104, 50);
            composerBar.Controls.Add(attachButton);
            composerBar.Controls.Add(composer);
            composerBar.Controls.Add(sendButton);
            composerBar.Resize += (_, _) => LayoutComposer();

            threadPanel.Controls.Add(messageScroll);
            threadPanel.Controls.Add(placeholder);
            threadPanel.Controls.Add(threadHeader);
            threadPanel.Controls.Add(composerBar);

            Surface.Controls.Add(threadPanel); // a kitöltő elem legyen elöl
            Surface.Controls.Add(listPanel);
            Surface.SizeChanged += (_, _) => UpdateLayout();

            statusTimer.Tick += (_, _) =>
            {
                statusTimer.Stop();
                headerStatus.ForeColor = Theme.Muted;
                headerStatus.Text = "";
            };

            Realtime.EventReceived += OnRealtime;
            UpdateLayout();
        }

        // ------------------------------------------------------------
        // Elrendezés
        // ------------------------------------------------------------

        private bool Wide => Surface.Width >= Ui.S(900);

        private void UpdateLayout()
        {
            if (Wide)
            {
                listPanel.Dock = DockStyle.Left;
                listPanel.Width = Ui.S(340);
                listPanel.Visible = true;
                threadPanel.Visible = true;
                backButton.Visible = false;
            }
            else
            {
                // keskeny ablaknál egyszerre csak a lista vagy a beszélgetés látszik
                listPanel.Dock = DockStyle.Fill;
                listPanel.Visible = !showThread;
                threadPanel.Visible = showThread;
                backButton.Visible = current != null;
            }
            LayoutHeader();
        }

        private void LayoutHeader()
        {
            int left = backButton.Visible ? Ui.S(72) : Ui.S(28);
            int nameHeight = Ui.LineHeight(headerName.Font);
            int statusHeight = Ui.LineHeight(headerStatus.Font);
            int top = (threadHeader.Height - nameHeight - statusHeight) / 2;
            headerName.Location = new Point(left, top);
            headerStatus.Location = new Point(left + Ui.S(2), top + nameHeight);
        }

        private void LayoutComposer()
        {
            int pad = Ui.S(16);
            int gap = Ui.S(10);
            int h = Ui.S(50);
            int y = (composerBar.Height - h) / 2;

            attachButton.Location = new Point(pad, y);
            sendButton.Location = new Point(composerBar.Width - pad - sendButton.Width, y);
            int left = attachButton.Right + gap;
            composer.SetBounds(left, y, Math.Max(sendButton.Left - gap - left, Ui.S(80)), h);
        }

        private void BackToList()
        {
            showThread = false;
            UpdateLayout();
        }

        // ------------------------------------------------------------
        // Betöltés
        // ------------------------------------------------------------

        protected override async Task BuildAsync()
        {
            ClearBody();
            await LoadConversationsAsync();
            if (IsDisposed) return;

            if (PendingConversationId is int id)
            {
                PendingConversationId = null;
                var target = conversations.FirstOrDefault(c => c.Id == id);
                if (target != null) await SelectAsync(target);
            }
        }

        protected override void ShowError(string message)
        {
            listMessage.Text = "Nem sikerült betölteni a beszélgetéseket.\n" + message;
            if (listScroll.Content.Controls.Contains(listMessage)) listScroll.Content.Controls.Remove(listMessage);
            Ui.Reset(listScroll.Content);
            listScroll.Content.Controls.Add(listMessage);
        }

        private async Task LoadConversationsAsync()
        {
            var list = await ApiClient.GetAsync<List<ConversationDto>>("/api/conversations");
            if (IsDisposed) return;

            conversations = list
                .OrderByDescending(c => c.LastMessageTime ?? DateTime.MinValue)
                .ThenByDescending(c => c.Id)
                .ToList();
            RenderList();
            AppEvents.RaiseBadgesStale();
        }

        private void RenderList()
        {
            var content = listScroll.Content;
            content.SuspendLayout();

            // az üzenet-címke nem szabad, hogy a Reset-tel megsemmisüljön
            if (content.Controls.Contains(listMessage)) content.Controls.Remove(listMessage);
            Ui.Reset(content);

            if (conversations.Count == 0)
            {
                listMessage.Text = me.Role == "STUDENT"
                    ? "Még nincs beszélgetésed. Az Oktatók oldalon az Üzenet gombbal indíthatsz egyet."
                    : "Még nem érkezett üzeneted. Ha egy diák ír neked, itt fog megjelenni.";
                content.Controls.Add(listMessage);
            }
            else
            {
                foreach (var conversation in conversations)
                {
                    var item = new ConversationItem(conversation);
                    item.SetSelected(current?.Id == conversation.Id);
                    item.Click += async (_, _) => await SelectAsync(conversation);
                    content.Controls.Add(item);
                }
                FitList();
            }

            content.ResumeLayout(true);
        }

        private void FitList()
        {
            int width = listScroll.Content.ClientSize.Width - listScroll.Content.Padding.Horizontal;
            foreach (Control control in listScroll.Content.Controls)
            {
                if (control is ConversationItem) control.Width = Math.Max(width, Ui.S(120));
            }
        }

        private async Task SelectAsync(ConversationDto conversation)
        {
            current = conversation;
            showThread = true;

            foreach (var item in listScroll.Content.Controls.OfType<ConversationItem>())
                item.SetSelected(item.Conversation.Id == conversation.Id);

            headerName.Text = conversation.OtherUserName;
            headerStatus.Text = "";
            placeholder.Visible = false;
            threadHeader.Visible = true;
            composerBar.Visible = true;
            messageScroll.Visible = true;
            UpdateLayout();
            LayoutComposer();

            await ReloadMessagesAsync();
            if (IsDisposed || current?.Id != conversation.Id) return;

            composer.FocusInput();
            await MarkReadAsync();
        }

        private async Task ReloadMessagesAsync()
        {
            var open = current;
            if (open == null) return;

            try
            {
                var list = await ApiClient.GetAsync<List<MessageDto>>($"/api/conversations/{open.Id}/messages");
                if (IsDisposed || current?.Id != open.Id) return;

                messages = list;
                RenderMessages();
            }
            catch (ApiException ex)
            {
                ShowStatus(ex.Message, error: true);
            }
        }

        // ------------------------------------------------------------
        // Üzenetek megjelenítése
        // ------------------------------------------------------------

        private void RenderMessages()
        {
            var content = messageScroll.Content;
            content.SuspendLayout();
            Ui.Reset(content);

            DateTime? lastDay = null;
            foreach (var message in messages)
            {
                AddMessageControl(message, ref lastDay);
            }

            content.ResumeLayout(true);
            FitMessages();
            ScrollToEnd();
        }

        private void AddMessageControl(MessageDto message, ref DateTime? lastDay)
        {
            var day = message.CreatedAt.ToLocalTime().Date;
            if (lastDay != day)
            {
                messageScroll.Content.Controls.Add(new DaySeparator(DayLabel(day)));
                lastDay = day;
            }

            var bubble = new MessageBubble(message, message.SenderId == me.Id);
            bubble.EditRequested += async b => await EditAsync(b.Message);
            bubble.RetractRequested += async b => await RetractAsync(b.Message);
            bubble.AttachmentRequested += async b => await DownloadAsync(b.Message);
            messageScroll.Content.Controls.Add(bubble);
        }

        private void AppendMessage(MessageDto message)
        {
            if (messages.Any(m => m.Id == message.Id)) return;

            bool stick = messageScroll.IsNearEnd() || message.SenderId == me.Id;
            messages.Add(message);

            DateTime? lastDay = messages.Count > 1 ? messages[^2].CreatedAt.ToLocalTime().Date : null;
            AddMessageControl(message, ref lastDay);
            FitMessages();
            if (stick) ScrollToEnd();
        }

        private void FitMessages()
        {
            var content = messageScroll.Content;
            int width = Math.Max(content.ClientSize.Width - content.Padding.Horizontal, Ui.S(160));
            foreach (Control control in content.Controls)
            {
                switch (control)
                {
                    case MessageBubble bubble:
                        bubble.SetRowWidth(width);
                        break;
                    case DaySeparator separator:
                        separator.Width = width;
                        break;
                }
            }
        }

        private void ScrollToEnd()
        {
            // a magasság a következő elrendezés után áll be, ezért késleltetve görgetünk
            BeginInvoke(new Action(() => messageScroll.ScrollBy(1e9)));
        }

        private static string DayLabel(DateTime day)
        {
            if (day == DateTime.Today) return "Ma";
            if (day == DateTime.Today.AddDays(-1)) return "Tegnap";
            return day.ToString("yyyy. MMMM d., dddd", Ui.Hu);
        }

        // ------------------------------------------------------------
        // Műveletek: küldés, csatolmány, szerkesztés, visszavonás
        // ------------------------------------------------------------

        private async Task SendAsync()
        {
            var open = current;
            string text = composer.Value.Trim();
            if (open == null || text.Length == 0 || busy) return;
            if (text.Length > 2000)
            {
                ShowStatus("Az üzenet legfeljebb 2000 karakter lehet.", error: true);
                return;
            }

            busy = true;
            composer.SetValue("");
            try
            {
                var message = await ApiClient.PostAsync<MessageDto>($"/api/conversations/{open.Id}/messages",
                    new Dictionary<string, object?> { ["content"] = text });
                if (IsDisposed) return;

                if (current?.Id == open.Id) AppendMessage(message);
                BumpConversation(open, message.Content, message.CreatedAt);
            }
            catch (ApiException ex)
            {
                composer.SetValue(text); // hiba esetén nem vész el a szöveg
                ShowStatus(ex.Message, error: true);
            }
            finally
            {
                busy = false;
            }
        }

        private async Task AttachAsync()
        {
            var open = current;
            if (open == null || busy) return;

            using var dialog = new OpenFileDialog
            {
                Title = "Fájl csatolása",
                Filter = "Támogatott fájlok|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt|Minden fájl|*.*"
            };
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

            busy = true;
            ShowStatus("Feltöltés...", error: false, autoHide: false);
            try
            {
                string caption = composer.Value.Trim();
                var message = await ApiClient.UploadAsync<MessageDto>($"/api/conversations/{open.Id}/attachments", dialog.FileName, caption);
                if (IsDisposed) return;

                composer.SetValue("");
                if (current?.Id == open.Id) AppendMessage(message);
                BumpConversation(open, message.Content, message.CreatedAt);
                ShowStatus("", error: false);
            }
            catch (ApiException ex)
            {
                ShowStatus(ex.Message, error: true);
            }
            finally
            {
                busy = false;
            }
        }

        private async Task DownloadAsync(MessageDto message)
        {
            using var dialog = new SaveFileDialog { FileName = message.AttachmentName ?? "fajl", Title = "Csatolmány mentése" };
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

            try
            {
                ShowStatus("Letöltés...", error: false, autoHide: false);
                await ApiClient.DownloadAsync($"/api/messages/{message.Id}/attachment", dialog.FileName);
                ShowStatus("A fájl elmentve.", error: false);

                try
                {
                    Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
                catch (Win32Exception)
                {
                    // nincs hozzá társított program: a fájl így is elmentve marad
                }
            }
            catch (ApiException ex)
            {
                ShowStatus(ex.Message, error: true);
            }
        }

        private async Task EditAsync(MessageDto message)
        {
            using var dialog = new EditMessageDialog(message.Content);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

            try
            {
                await ApiClient.PatchAsync($"/api/messages/{message.Id}", new Dictionary<string, object?> { ["content"] = dialog.NewText });
                await ReloadMessagesAsync();
                await LoadConversationsAsync();
            }
            catch (ApiException ex)
            {
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
                await ReloadMessagesAsync();
                await LoadConversationsAsync();
            }
            catch (ApiException ex)
            {
                ShowStatus(ex.Message, error: true);
            }
        }

        private async Task MarkReadAsync()
        {
            var open = current;
            if (open == null || open.UnreadCount == 0) return;

            open.UnreadCount = 0;
            RenderList();
            try
            {
                await ApiClient.PostAsync($"/api/conversations/{open.Id}/read");
                AppEvents.RaiseBadgesStale();
            }
            catch (ApiException)
            {
                // nem kritikus: a következő frissítéskor újra próbálkozik
            }
        }

        /// <summary>A beszélgetés előnézetét és idejét frissíti, és előre teszi a listában.</summary>
        private void BumpConversation(ConversationDto conversation, string preview, DateTime time)
        {
            conversation.LastMessage = preview;
            conversation.LastMessageTime = time;
            conversation.LastMessageRetractedValue = 0;

            conversations = conversations
                .OrderByDescending(c => c.LastMessageTime ?? DateTime.MinValue)
                .ThenByDescending(c => c.Id)
                .ToList();
            RenderList();
        }

        // ------------------------------------------------------------
        // Valós idejű események
        // ------------------------------------------------------------

        private async void OnRealtime(RealtimeEvent e)
        {
            if (IsDisposed) return;

            try
            {
                switch (e.Type)
                {
                    case "new_message" when e.Message != null:
                        await OnNewMessageAsync(e.Message);
                        break;

                    case "message_retracted":
                    case "message_edited":
                        if (current?.Id == e.ConversationId) await ReloadMessagesAsync();
                        await LoadConversationsAsync();
                        break;

                    case "typing":
                        if (current?.Id == e.ConversationId) ShowStatus("ír...", error: false);
                        break;
                }
            }
            catch (ApiException)
            {
                // a következő esemény vagy frissítés kijavítja
            }
        }

        private async Task OnNewMessageAsync(MessageDto message)
        {
            var conversation = conversations.FirstOrDefault(c => c.Id == message.ConversationId);

            if (current?.Id == message.ConversationId)
            {
                AppendMessage(message);
                if (conversation != null) BumpConversation(conversation, message.Content, message.CreatedAt);
                await ApiClient.PostAsync($"/api/conversations/{message.ConversationId}/read");
                AppEvents.RaiseBadgesStale();
            }
            else if (conversation != null)
            {
                conversation.UnreadCount++;
                BumpConversation(conversation, message.Content, message.CreatedAt);
                AppEvents.RaiseBadgesStale();
            }
            else
            {
                await LoadConversationsAsync(); // új beszélgetés (az oktatónál az első üzenettel jelenik meg)
            }
        }

        private void NotifyTyping()
        {
            if (current == null) return;

            long now = Environment.TickCount64;
            if (now - lastTypingSent < 2000) return; // legfeljebb 2 másodpercenként jelzünk
            lastTypingSent = now;
            Realtime.SendTyping(current.Id);
        }

        private void ShowStatus(string text, bool error, bool autoHide = true)
        {
            headerStatus.ForeColor = error ? Theme.Danger : Theme.Muted;
            headerStatus.Text = text;
            statusTimer.Stop();
            if (autoHide && text.Length > 0) statusTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Realtime.EventReceived -= OnRealtime;
                statusTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ================================================================
    // Vezérlők
    // ================================================================

    /// <summary>Egy beszélgetés a listában: avatar, név, előnézet, idő és olvasatlan jelvény.</summary>
    internal sealed class ConversationItem : Control
    {
        public readonly ConversationDto Conversation;
        private bool selected;
        private double hoverT;
        private Anim.Tween? hoverTween;

        public ConversationItem(ConversationDto conversation)
        {
            Conversation = conversation;
            Height = Ui.S(80);
            Margin = Ui.Pad(0, 0, 0, 2);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        public void SetSelected(bool value)
        {
            selected = value;
            Invalidate();
        }

        private void HoverTo(double to)
        {
            hoverTween?.Cancel();
            double from = hoverT;
            hoverTween = Anim.Run(140, t =>
            {
                hoverT = from + (to - from) * t;
                Invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e) { HoverTo(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { HoverTo(0); base.OnMouseLeave(e); }

        private static string ShortTime(DateTime? time)
        {
            if (time == null) return "";
            var local = time.Value.ToLocalTime();
            if (local.Date == DateTime.Today) return local.ToString("HH:mm", Ui.Hu);
            if (local.Date == DateTime.Today.AddDays(-1)) return "Tegnap";
            return local.ToString("MM. dd.", Ui.Hu);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var card = new Rectangle(Ui.S(4), 0, Width - Ui.S(8), Height - 1);
            Color fill = selected ? Theme.ChipBg : Anim.Lerp(Theme.CardBg, Theme.SoftHover, hoverT);
            using (var path = Ui.RoundedPath(card, Ui.S(14)))
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, path);

            int avatarSize = Ui.S(48);
            var avatar = new Rectangle(card.X + Ui.S(14), (Height - avatarSize) / 2, avatarSize, avatarSize);
            Ui.DrawAvatar(g, avatar, Ui.Initials(Conversation.OtherUserName), Theme.MainBlue, Theme.White);

            int textX = avatar.Right + Ui.S(14);
            int right = card.Right - Ui.S(14);
            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
            using var rightFormat = new StringFormat(StringFormatFlags.NoWrap) { Alignment = StringAlignment.Far };

            // idő jobb felül
            string time = ShortTime(Conversation.LastMessageTime);
            var timeFont = Ui.MakeFont(8.5F);
            int timeWidth = time.Length == 0 ? 0 : TextRenderer.MeasureText(time, timeFont).Width + Ui.S(4);
            using (var timeBrush = new SolidBrush(Theme.Muted))
                g.DrawString(time, timeFont, timeBrush, new Rectangle(right - timeWidth, Ui.S(14), timeWidth, Ui.S(20)), rightFormat);

            // név
            bool unread = Conversation.UnreadCount > 0;
            var nameFont = Ui.MakeFont(11F, FontStyle.Bold);
            using (var nameBrush = new SolidBrush(Theme.Text))
                g.DrawString(Conversation.OtherUserName, nameFont, nameBrush,
                    new Rectangle(textX, Ui.S(12), Math.Max(right - timeWidth - textX - Ui.S(6), 0), Ui.S(26)), format);

            // előnézet
            string preview = Conversation.LastMessageRetracted ? "Üzenet visszavonva" : (Conversation.LastMessage ?? "Még nincs üzenet");
            var previewFont = Ui.MakeFont(9.5F, unread ? FontStyle.Bold : FontStyle.Regular);
            int badgeSpace = unread ? Ui.S(34) : 0;
            using (var previewBrush = new SolidBrush(unread ? Theme.Text : Theme.Muted))
                g.DrawString(preview.Replace('\n', ' '), previewFont, previewBrush,
                    new Rectangle(textX, Ui.S(44), Math.Max(right - textX - badgeSpace, 0), Ui.S(24)), format);

            // olvasatlan jelvény
            if (unread)
            {
                string count = Conversation.UnreadCount > 99 ? "99+" : Conversation.UnreadCount.ToString();
                var badgeFont = Ui.MakeFont(8.5F, FontStyle.Bold);
                int badgeWidth = Math.Max(Ui.S(24), TextRenderer.MeasureText(count, badgeFont).Width + Ui.S(12));
                var badge = new Rectangle(right - badgeWidth, Ui.S(44), badgeWidth, Ui.S(24));
                using (var badgePath = Ui.RoundedPath(badge, Ui.S(12)))
                using (var badgeBrush = new SolidBrush(Theme.MainBlue))
                    g.FillPath(badgeBrush, badgePath);
                using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using var white = new SolidBrush(Theme.White);
                g.DrawString(count, badgeFont, white, badge, center);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) hoverTween?.Cancel();
            base.Dispose(disposing);
        }
    }

    /// <summary>Napi elválasztó az üzenetek között ("Ma", "Tegnap", dátum).</summary>
    internal sealed class DaySeparator : Control
    {
        private readonly string text;

        public DaySeparator(string text)
        {
            this.text = text;
            Height = Ui.S(44);
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var font = Ui.MakeFont(9F, FontStyle.Bold);
            var size = TextRenderer.MeasureText(text, font);
            int width = size.Width + Ui.S(28);
            int height = size.Height + Ui.S(10);
            var pill = new Rectangle((Width - width) / 2, (Height - height) / 2, width, height);

            using var path = Ui.RoundedPath(pill, height / 2);
            using var brush = new SolidBrush(Theme.ChipBg);
            g.FillPath(brush, path);
            using var textBrush = new SolidBrush(Theme.ChipFg);
            using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, textBrush, pill, center);
        }
    }

    /// <summary>
    /// Üzenetbuborék: a saját üzenetek jobbra (kék), a másik félé balra. Szöveg, csatolmány, idő, "szerkesztve"
    /// és olvasottság-jelzés. Jobb gombbal: szerkesztés, visszavonás, másolás.
    /// </summary>
    internal sealed class MessageBubble : Control
    {
        private const TextFormatFlags TextFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl;

        public readonly MessageDto Message;
        private readonly bool mine;
        private readonly bool showText;
        private readonly Font textFont = Ui.MakeFont(10.5F);
        private readonly Font metaFont = Ui.MakeFont(8.5F);
        private ContextMenuStrip? menu;

        private Rectangle bubble;
        private Rectangle textRect;
        private Rectangle attachRect;
        private Rectangle metaRect;
        private string metaText = "";

        public event Action<MessageBubble>? EditRequested;
        public event Action<MessageBubble>? RetractRequested;
        public event Action<MessageBubble>? AttachmentRequested;

        public MessageBubble(MessageDto message, bool mine)
        {
            Message = message;
            this.mine = mine;
            showText = !message.IsRetracted && !(message.HasAttachment && message.Content == message.AttachmentName) && message.Content.Length > 0;
            if (message.IsRetracted) textFont = Ui.MakeFont(10F, FontStyle.Italic);

            Margin = Ui.Pad(0);
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        private string DisplayText => Message.IsRetracted ? "Üzenet visszavonva" : Message.Content;

        /// <summary>A sor szélességéhez igazítja a buborékot, és kiszámolja a magasságát.</summary>
        public void SetRowWidth(int rowWidth)
        {
            int padX = Ui.S(14);
            int padY = Ui.S(10);
            int maxContent = Math.Max((int)(rowWidth * 0.72) - padX * 2, Ui.S(120));

            var local = Message.CreatedAt.ToLocalTime();
            metaText = local.ToString("HH:mm", Ui.Hu);
            if (Message.EditedAt != null && !Message.IsRetracted) metaText += " · szerkesztve";
            if (mine && !Message.IsRetracted) metaText += Message.IsRead ? "  ✓✓" : "  ✓";
            var metaSize = TextRenderer.MeasureText(metaText, metaFont);

            var textSize = Size.Empty;
            bool hasText = showText || Message.IsRetracted;
            if (hasText)
                textSize = TextRenderer.MeasureText(DisplayText, textFont, new Size(maxContent, 0), TextFlags);

            int attachWidth = 0;
            int attachHeight = Ui.S(44);
            bool hasAttachment = Message.HasAttachment && !Message.IsRetracted;
            if (hasAttachment)
            {
                var nameSize = TextRenderer.MeasureText(Message.AttachmentName ?? "", Ui.MakeFont(9.5F, FontStyle.Bold));
                attachWidth = Math.Min(maxContent, Math.Max(Ui.S(190), nameSize.Width + Ui.S(64)));
            }

            int contentWidth = Math.Max(Math.Max(textSize.Width, attachWidth), metaSize.Width);
            int bubbleWidth = contentWidth + padX * 2;
            int y = padY;
            int textHeight = hasText ? textSize.Height : 0;

            int bubbleHeight = padY + textHeight;
            if (hasAttachment) bubbleHeight += (hasText ? Ui.S(8) : 0) + attachHeight;
            bubbleHeight += Ui.S(4) + metaSize.Height + padY;

            int bubbleX = mine ? rowWidth - bubbleWidth - Ui.S(4) : Ui.S(4);
            bubble = new Rectangle(bubbleX, Ui.S(3), bubbleWidth, bubbleHeight);

            int cursor = bubble.Y + y;
            textRect = hasText ? new Rectangle(bubble.X + padX, cursor, contentWidth, textHeight) : Rectangle.Empty;
            if (hasText) cursor += textHeight;
            if (hasAttachment)
            {
                cursor += hasText ? Ui.S(8) : 0;
                attachRect = new Rectangle(bubble.X + padX, cursor, contentWidth, attachHeight);
                cursor += attachHeight;
            }
            else
            {
                attachRect = Rectangle.Empty;
            }
            metaRect = new Rectangle(bubble.X + padX, cursor + Ui.S(4), contentWidth, metaSize.Height);

            Width = rowWidth;
            Height = bubble.Height + Ui.S(8);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            if (bubble.IsEmpty) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool retracted = Message.IsRetracted;
            Color fill = mine && !retracted ? Theme.MainBlue : Theme.CardBg;
            Color textColor = mine && !retracted ? Theme.White : (retracted ? Theme.Muted : Theme.Text);
            Color metaColor = mine && !retracted ? Color.FromArgb(200, Color.White) : Theme.Muted;

            using (var path = Ui.RoundedPath(bubble, Ui.S(16)))
            {
                using var brush = new SolidBrush(fill);
                g.FillPath(brush, path);
                if (!(mine && !retracted))
                {
                    using var border = new Pen(Theme.CardBorder);
                    g.DrawPath(border, path);
                }
            }

            if (!textRect.IsEmpty)
                TextRenderer.DrawText(g, DisplayText, textFont, textRect, textColor, TextFlags);

            if (!attachRect.IsEmpty)
                PaintAttachment(g, textColor);

            TextRenderer.DrawText(g, metaText, metaFont, metaRect, metaColor,
                TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private void PaintAttachment(Graphics g, Color textColor)
        {
            Color chip = mine ? Color.FromArgb(48, Color.White) : Theme.SoftHover;
            using (var path = Ui.RoundedPath(attachRect, Ui.S(10)))
            using (var brush = new SolidBrush(chip))
                g.FillPath(brush, path);

            var icon = new Rectangle(attachRect.X + Ui.S(8), attachRect.Y, Ui.S(32), attachRect.Height);
            using (var iconBrush = new SolidBrush(textColor))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("", Ui.IconFont(14F), iconBrush, icon, center);

            var nameRect = new Rectangle(icon.Right + Ui.S(4), attachRect.Y + Ui.S(5), attachRect.Right - icon.Right - Ui.S(12), Ui.S(20));
            TextRenderer.DrawText(g, Message.AttachmentName ?? "fájl", Ui.MakeFont(9.5F, FontStyle.Bold), nameRect, textColor,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            var sizeRect = new Rectangle(nameRect.X, nameRect.Bottom - Ui.S(1), nameRect.Width, Ui.S(18));
            TextRenderer.DrawText(g, FormatSize(Message.AttachmentSize), metaFont, sizeRect,
                mine ? Color.FromArgb(200, Color.White) : Theme.Muted, TextFormatFlags.NoPadding);
        }

        private static string FormatSize(long? bytes)
        {
            if (bytes == null) return "";
            double value = bytes.Value;
            if (value < 1024) return $"{value:0} B";
            if (value < 1024 * 1024) return $"{value / 1024:0.#} KB";
            return $"{value / (1024 * 1024):0.#} MB";
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = !attachRect.IsEmpty && attachRect.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && !attachRect.IsEmpty && attachRect.Contains(e.Location))
                AttachmentRequested?.Invoke(this);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Right || Message.IsRetracted || !bubble.Contains(e.Location)) return;

            menu?.Dispose();
            menu = new ContextMenuStrip();
            menu.Items.Add("Másolás", null, (_, _) =>
            {
                if (Message.Content.Length > 0) Clipboard.SetText(Message.Content);
            });
            if (mine)
            {
                if (!Message.HasAttachment)
                    menu.Items.Add("Szerkesztés", null, (_, _) => EditRequested?.Invoke(this));
                menu.Items.Add("Visszavonás", null, (_, _) => RetractRequested?.Invoke(this));
            }
            menu.Show(this, e.Location);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) menu?.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Üzenetíró mező: lekerekített "pirula", Enterre küld.</summary>
    internal sealed class ComposerBox : Control
    {
        private readonly TextBox input;
        private double focusT;
        private Anim.Tween? focusTween;

        public event EventHandler? SendRequested;
        public event EventHandler? TextEdited;

        public ComposerBox(string placeholder)
        {
            Cursor = Cursors.IBeam;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            input = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Ui.MakeFont(11F),
                PlaceholderText = placeholder,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                MaxLength = 2000
            };
            input.GotFocus += (_, _) => FocusTo(1);
            input.LostFocus += (_, _) => FocusTo(0);
            input.TextChanged += (_, _) => TextEdited?.Invoke(this, EventArgs.Empty);
            input.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true; // ne csipogjon
                    SendRequested?.Invoke(this, EventArgs.Empty);
                }
            };
            Controls.Add(input);
        }

        public string Value => input.Text;

        public void SetValue(string text) => input.Text = text;

        public void FocusInput() => input.Focus();

        private void FocusTo(double to)
        {
            focusTween?.Cancel();
            double from = focusT;
            focusTween = Anim.Run(160, t =>
            {
                focusT = from + (to - from) * t;
                Invalidate();
            });
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (input is null) return;

            input.Left = Ui.S(20);
            input.Width = Math.Max(Width - Ui.S(40), 20);
            input.Top = (Height - input.Height) / 2;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            input.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2);
            using var fill = new SolidBrush(Theme.InputBg);
            using var border = new Pen(Anim.Lerp(Theme.CardBorder, Theme.MainBlue, focusT), 1F + (float)focusT);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) focusTween?.Cancel();
            base.Dispose(disposing);
        }
    }

    /// <summary>Saját üzenet szövegének szerkesztése.</summary>
    internal sealed class EditMessageDialog : DialogBase
    {
        private readonly TextBox box;

        public string NewText { get; private set; } = "";

        public EditMessageDialog(string text)
            : base("Üzenet szerkesztése", "A módosított üzenet mellett \"szerkesztve\" jelzés jelenik meg.", "Mentés", Ui.Sz(560, 400))
        {
            box = NewTextBox(170, 2000);
            box.Text = text;
            AddBody(box);
            SetFooter("");
        }

        protected override Task OnActionAsync()
        {
            string text = box.Text.Trim();
            if (text.Length == 0)
            {
                SetFooter("Az üzenet nem lehet üres.", error: true);
                return Task.CompletedTask;
            }

            NewText = text;
            DialogResult = DialogResult.OK;
            Close();
            return Task.CompletedTask;
        }
    }
}

using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
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
        private readonly HoverButton emojiButton;
        private readonly HoverButton scrollDownButton;
        private readonly HoverButton sendButton;
        private readonly HoverButton profileButton;
        private readonly SearchBox listSearch;
        private readonly Debouncer filterDebounce;
        private readonly ComposerBox composer;
        private readonly System.Windows.Forms.Timer statusTimer = new() { Interval = 3000 };
        private readonly System.Windows.Forms.Timer receiptTimer = new() { Interval = 8000 };
        private bool receiptsBusy;

        /// <summary>Az el nem küldött üzenetek beszélgetésenként: oldalváltás vagy témaváltás után sem vesznek el.</summary>
        private static readonly Dictionary<int, string> Drafts = new();

        private static readonly Font EmojiButtonFont = new("Segoe UI Emoji", 14F, FontStyle.Regular, GraphicsUnit.Point);

        /// <summary>Üzenetek száma, amennyit egyszerre megjelenítünk (a régebbiek gombra töltődnek be).</summary>
        private const int MessagePageSize = 60;

        private List<MessageDto> messages = new();
        private int shownFrom;
        private int? restoreConversationId;
        private string listFilter = "";
        private ConversationDto? current;
        private bool showThread;
        private bool busy;
        private long lastTypingSent;
        private bool restoringDraft;

        /// <summary>
        /// A saját szerkesztésről a szerver nekünk is küld eseményt; ezt nem kell újratöltéssel kezelni,
        /// mert a módosítást már helyben alkalmaztuk. Üzenetazonosító → a szerkesztés időpontja.
        /// </summary>
        private readonly Dictionary<int, long> selfEdits = new();
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

            var listHeader = new Panel { Dock = DockStyle.Top, Height = Ui.S(142), BackColor = Theme.CardBg };
            var listTitle = Ui.Lbl("Üzenetek", 17F, Theme.Text, bold: true);
            listTitle.Location = Ui.Pt(24, 20);
            listHeader.Controls.Add(listTitle);

            // keresés a beszélgetések között (név és utolsó üzenet alapján, ékezetek nélkül is);
            // a lista a gépelés szünetében frissül, nem minden betűnél
            filterDebounce = new Debouncer(150, RenderList);
            listSearch = new SearchBox("Keresés a beszélgetések között...") { Margin = Ui.Pad(0) };
            listSearch.ValueChanged += (_, _) =>
            {
                listFilter = listSearch.Value.Trim();
                filterDebounce.Trigger();
            };
            listHeader.Controls.Add(listSearch);

            // a diák a Chat oldalról is indíthat új beszélgetést
            HoverButton? newButton = null;
            if (me.Role == "STUDENT")
            {
                newButton = Ui.PrimaryBtn("+ Új", async (_, _) => await NewConversationAsync());
                newButton.AutoSize = false;
                newButton.Size = Ui.Sz(86, 40);
                newButton.Margin = Ui.Pad(0);
                listHeader.Controls.Add(newButton);
            }

            listHeader.Resize += (_, _) =>
            {
                listSearch.SetBounds(Ui.S(16), Ui.S(78), Math.Max(listHeader.Width - Ui.S(32), Ui.S(120)), Ui.S(50));
                if (newButton != null)
                    newButton.Location = new Point(listHeader.Width - newButton.Width - Ui.S(20), Ui.S(18));
            };
            listSearch.SetBounds(Ui.S(16), Ui.S(78), Ui.S(308), Ui.S(50));
            if (newButton != null)
                newButton.Location = new Point(Ui.S(340) - newButton.Width - Ui.S(20), Ui.S(18));

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

            // "ugrás az aljára" gomb: akkor látszik, ha nem a legutolsó üzeneteknél jársz
            scrollDownButton = Ui.PrimaryBtn("\uE74B", (_, _) => messageScroll.ScrollBy(1e9));
            scrollDownButton.Font = Ui.IconFont(13F);
            scrollDownButton.AccessibleName = "Ugrás a legutolsó üzenethez";
            scrollDownButton.AutoSize = false;
            scrollDownButton.Size = Ui.Sz(46, 46);
            scrollDownButton.Margin = Ui.Pad(0);
            scrollDownButton.Visible = false;
            messageScroll.Controls.Add(scrollDownButton);
            scrollDownButton.BringToFront();
            messageScroll.Scrolled += (_, _) => UpdateScrollDownButton();
            messageScroll.Resize += (_, _) => UpdateScrollDownButton();
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
            profileButton = Ui.Btn("Profil megtekintése", Theme.CardBg, Theme.MainBlue, async (_, _) => await OpenProfileAsync(), outline: true);
            profileButton.AutoSize = false;
            profileButton.Size = Ui.Sz(190, 40);
            profileButton.Margin = Ui.Pad(0);
            profileButton.Visible = false;
            threadHeader.Controls.Add(backButton);
            threadHeader.Controls.Add(headerName);
            threadHeader.Controls.Add(headerStatus);
            threadHeader.Controls.Add(profileButton);
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
            emojiButton = Ui.Btn("", Theme.CardBg, Theme.Text, (_, _) => ShowEmojiPicker(), outline: true);
            emojiButton.Font = EmojiButtonFont;
            emojiButton.Text = "😊";
            emojiButton.AccessibleName = "Emoji beszúrása";
            emojiButton.AutoSize = false;
            emojiButton.Size = Ui.Sz(50, 50);
            composer = new ComposerBox("Írj üzenetet...");
            composer.FileDropped += async path => await UploadFileAsync(path, confirm: true);
            composer.SendRequested += async (_, _) => await SendAsync();
            composer.TextEdited += (_, _) =>
            {
                if (restoringDraft) return;
                SaveDraft();
                NotifyTyping();
            };
            sendButton = Ui.PrimaryBtn("Küldés", async (_, _) => await SendAsync());
            sendButton.AutoSize = false;
            sendButton.Size = Ui.Sz(104, 50);
            composerBar.Controls.Add(attachButton);
            composerBar.Controls.Add(emojiButton);
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

            // a szerver nem küld eseményt az olvasásról, ezért amíg van olvasatlan saját üzenetünk, időnként rákérdezünk
            receiptTimer.Tick += async (_, _) => await RefreshReceiptsAsync();
            receiptTimer.Start();

            Realtime.EventReceived += OnRealtime;
            ChatState.Changed += OnChatStateChanged;
            UpdateLayout();

            // fájlok behúzása az üzenetek vagy a szövegmező fölé
            foreach (var target in new Control[] { threadPanel, messageScroll, messageScroll.Content, composerBar })
                HookDrop(target);
            composer.AttachDrop(HandleDragEnter, HandleDragDrop);

            if (ChatState.Loaded) RenderList(); // a már ismert beszélgetések azonnal látszanak
        }

        private void HookDrop(Control target)
        {
            target.AllowDrop = true;
            target.DragEnter += HandleDragEnter;
            target.DragDrop += HandleDragDrop;
        }

        private void HandleDragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = current != null && e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private async void HandleDragDrop(object? sender, DragEventArgs e)
        {
            if (current != null && e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
                await UploadFileAsync(files[0], confirm: true);
        }

        private void ShowEmojiPicker() =>
            EmojiPicker.ShowAbove(emojiButton, emoji => composer.InsertText(emoji));

        private void UpdateScrollDownButton()
        {
            if (IsDisposed) return;
            scrollDownButton.Location = new Point(
                Math.Max(messageScroll.Width - scrollDownButton.Width - Ui.S(34), 0),
                Math.Max(messageScroll.Height - scrollDownButton.Height - Ui.S(16), 0));
            scrollDownButton.Visible = messageScroll.Visible && messages.Count > 0 && !messageScroll.IsNearEnd();
            if (scrollDownButton.Visible) scrollDownButton.BringToFront();
        }

        // ------------------------------------------------------------
        // Állapot átvitele újraépítéskor (témaváltás, másik monitor)
        // ------------------------------------------------------------

        public override object? CaptureState() => current?.Id;

        public override void RestoreState(object? state)
        {
            if (state is int id) restoreConversationId = id;
        }

        private void OnChatStateChanged()
        {
            if (IsDisposed) return;

            // a lista cseréjekor új objektumok jönnek létre: a megnyitott beszélgetés hivatkozását frissítjük
            if (current != null)
                current = ChatState.Find(current.Id) ?? current;
            RenderList();
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
            SyncOpenState();
            LayoutHeader();
        }

        /// <summary>Igaz, ha egy beszélgetés üzenetei éppen látszanak a képernyőn.</summary>
        private bool ThreadVisible => current != null && (Wide || showThread);

        private void SyncOpenState() => ChatState.OpenConversationId = ThreadVisible ? current!.Id : null;

        private void LayoutHeader()
        {
            int left = backButton.Visible ? Ui.S(72) : Ui.S(28);
            int nameHeight = Ui.LineHeight(headerName.Font);
            int statusHeight = Ui.LineHeight(headerStatus.Font);
            int top = (threadHeader.Height - nameHeight - statusHeight) / 2;
            headerName.Location = new Point(left, top);
            headerStatus.Location = new Point(left + Ui.S(2), top + nameHeight);
            profileButton.Location = new Point(Math.Max(threadHeader.Width - profileButton.Width - Ui.S(20), left), (threadHeader.Height - profileButton.Height) / 2);
        }

        private void LayoutComposer()
        {
            int pad = Ui.S(16);
            int gap = Ui.S(10);
            int h = Ui.S(50);
            int y = (composerBar.Height - h) / 2;

            attachButton.Location = new Point(pad, y);
            emojiButton.Location = new Point(attachButton.Right + gap, y);
            sendButton.Location = new Point(composerBar.Width - pad - sendButton.Width, y);
            int left = emojiButton.Right + gap;
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
            await ChatState.LoadAsync();
            if (IsDisposed) return;

            // elsőbbség: az Oktatók oldalról kért beszélgetés, utána az újraépítés előtt nyitott
            int? wanted = PendingConversationId ?? restoreConversationId;
            PendingConversationId = null;
            restoreConversationId = null;

            if (wanted is int id && ChatState.Find(id) is { } target)
                await SelectAsync(target);
        }

        protected override void ShowError(string message)
        {
            listMessage.Text = "Nem sikerült betölteni a beszélgetéseket.\n" + message;
            if (listScroll.Content.Controls.Contains(listMessage)) listScroll.Content.Controls.Remove(listMessage);
            Ui.Reset(listScroll.Content);
            listScroll.Content.Controls.Add(listMessage);
        }

        private void RenderList()
        {
            var content = listScroll.Content;

            // ugyanaz a lista (azonos objektumok, azonos sorrend): csak újrafestjük, nem építjük újra az elemeket
            var filtered = FilteredConversations();
            var existing = content.Controls.OfType<ConversationItem>().ToList();
            if (filtered.Count > 0 && existing.Count == filtered.Count
                && existing.Zip(filtered).All(pair => ReferenceEquals(pair.First.Conversation, pair.Second)))
            {
                foreach (var item in existing)
                {
                    item.SetSelected(current?.Id == item.Conversation.Id);
                    item.RefreshState();
                }
                return;
            }

            content.SuspendLayout();

            // az üzenet-címke nem szabad, hogy a Reset-tel megsemmisüljön
            if (content.Controls.Contains(listMessage)) content.Controls.Remove(listMessage);
            Ui.Reset(content);

            var conversations = filtered;
            if (conversations.Count == 0)
            {
                listMessage.Text = ChatState.Conversations.Count > 0
                    ? "Nincs a keresésnek megfelelő beszélgetés."
                    : me.Role == "STUDENT"
                        ? "Még nincs beszélgetésed. A + Új gombbal indíthatsz egyet."
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

        private List<ConversationDto> FilteredConversations()
        {
            var all = ChatState.Conversations;
            if (listFilter.Length == 0) return all.ToList();

            return all.Where(c => Ui.ContainsText(c.OtherUserName, listFilter) || Ui.ContainsText(c.LastMessage ?? "", listFilter)).ToList();
        }

        private void FitList()
        {
            var content = listScroll.Content;
            int width = Math.Max(content.ClientSize.Width - content.Padding.Horizontal, Ui.S(120));

            content.SuspendLayout();
            foreach (Control control in content.Controls)
            {
                if (control is ConversationItem && control.Width != width) control.Width = width;
            }
            content.ResumeLayout(true);
        }

        private async Task SelectAsync(ConversationDto conversation)
        {
            current = conversation;
            showThread = true;

            foreach (var item in listScroll.Content.Controls.OfType<ConversationItem>())
                item.SetSelected(item.Conversation.Id == conversation.Id);

            headerName.Text = conversation.OtherUserName;
            headerStatus.Text = "";
            profileButton.Visible = true;
            placeholder.Visible = false;
            threadHeader.Visible = true;
            composerBar.Visible = true;
            messageScroll.Visible = true;
            UpdateLayout();
            LayoutComposer();

            // az el nem küldött szöveg visszatöltése (a beállítás közben nem számít szerkesztésnek)
            restoringDraft = true;
            composer.SetValue(Drafts.TryGetValue(conversation.Id, out var draft) ? draft : "");
            restoringDraft = false;

            await ReloadMessagesAsync(scrollToEnd: true, firstLoad: true);
            if (IsDisposed || current?.Id != conversation.Id) return;

            composer.FocusInput();
            await MarkReadAsync();
        }

        private async Task ReloadMessagesAsync(bool scrollToEnd, bool firstLoad = false)
        {
            var open = current;
            if (open == null) return;

            try
            {
                var list = await ApiClient.GetAsync<List<MessageDto>>($"/api/conversations/{open.Id}/messages");
                if (IsDisposed || current?.Id != open.Id) return;

                messages = list;
                // új beszélgetésnél az utolsó oldalnyi üzenet látszik, újratöltéskor a már megnyitott régebbiek is
                shownFrom = firstLoad ? Math.Max(0, messages.Count - MessagePageSize) : Math.Min(shownFrom, Math.Max(0, messages.Count - MessagePageSize));
                RenderMessages(scrollToEnd);
            }
            catch (ApiException ex)
            {
                ShowStatus(ex.Message, error: true);
            }
        }

        // ------------------------------------------------------------
        // Üzenetek megjelenítése
        // ------------------------------------------------------------

        private void RenderMessages(bool scrollToEnd)
        {
            var content = messageScroll.Content;
            content.SuspendLayout();
            Ui.Reset(content);

            // régebbi üzenetek: csak kérésre jelennek meg, így hosszú beszélgetésnél sem lassul a felület
            if (shownFrom > 0)
            {
                var older = Ui.Btn($"Korábbi üzenetek ({shownFrom})", Theme.CardBg, Theme.Text, (_, _) => BeginInvoke(new Action(ShowOlderMessages)), outline: true);
                older.Tag = "older";
                content.Controls.Add(older);
            }

            DateTime? lastDay = null;
            for (int i = shownFrom; i < messages.Count; i++)
                AddMessageControl(messages[i], ref lastDay);

            FitMessages(); // még felfüggesztett elrendezésnél: egyszer számolódik újra
            content.ResumeLayout(true);
            if (scrollToEnd) ScrollToEnd();
        }

        /// <summary>Még egy oldalnyi régebbi üzenet; az olvasott rész a képernyőn ugyanott marad.</summary>
        private void ShowOlderMessages()
        {
            double offset = messageScroll.Offset;
            int oldHeight = messageScroll.Content.Height;

            shownFrom = Math.Max(0, shownFrom - MessagePageSize);
            RenderMessages(scrollToEnd: false);

            messageScroll.JumpTo(offset + (messageScroll.Content.Height - oldHeight));
        }

        private MessageBubble CreateBubble(MessageDto message)
        {
            var bubble = new MessageBubble(message, message.SenderId == me.Id);
            bubble.EditRequested += async b => await EditAsync(b.Message);
            bubble.RetractRequested += async b => await RetractAsync(b.Message);
            bubble.AttachmentRequested += async b => await OpenAttachmentAsync(b.Message, open: false);
            bubble.AttachmentOpenRequested += async b => await OpenAttachmentAsync(b.Message, open: true);
            return bubble;
        }

        private void AddMessageControl(MessageDto message, ref DateTime? lastDay)
        {
            var day = message.CreatedAt.ToLocalTime().Date;
            if (lastDay != day)
            {
                messageScroll.Content.Controls.Add(new DaySeparator(DayLabel(day)));
                lastDay = day;
            }

            messageScroll.Content.Controls.Add(CreateBubble(message));
        }

        /// <summary>Egyetlen üzenet buborékját cseréli le (szerkesztés / visszavonás után), az egész lista újraépítése nélkül.</summary>
        private void ReplaceBubble(MessageDto message)
        {
            var content = messageScroll.Content;
            var old = content.Controls.OfType<MessageBubble>().FirstOrDefault(b => b.Message.Id == message.Id);
            if (old == null) return;

            int index = content.Controls.GetChildIndex(old);
            var replacement = CreateBubble(message);

            content.SuspendLayout();
            content.Controls.Add(replacement);
            content.Controls.SetChildIndex(replacement, index);
            content.Controls.Remove(old);
            old.Dispose();
            replacement.SetRowWidth(Math.Max(content.ClientSize.Width - content.Padding.Horizontal, Ui.S(160)));
            content.ResumeLayout(true);
        }

        /// <summary>A beszélgetés előnézetét igazítja, ha a módosított üzenet az utolsó volt.</summary>
        private void RefreshPreviewIfLast(MessageDto message)
        {
            var open = current;
            if (open == null || messages.Count == 0 || messages[^1].Id != message.Id) return;
            ChatState.UpdatePreview(open, message.IsRetracted ? "" : message.Content, message.IsRetracted);
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
            else UpdateScrollDownButton();
        }

        private void FitMessages()
        {
            var content = messageScroll.Content;
            int width = Math.Max(content.ClientSize.Width - content.Padding.Horizontal, Ui.S(160));

            content.SuspendLayout();
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
                    case HoverButton { Tag: "older" } older:
                        older.Margin = new Padding(Math.Max((width - older.Width) / 2, 0), Ui.S(6), 0, Ui.S(10));
                        break;
                }
            }
            content.ResumeLayout(true);
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
                ChatState.Bump(open, message.Content, message.CreatedAt);
            }
            catch (ApiException ex)
            {
                if (!IsDisposed && current?.Id == open.Id) composer.SetValue(text); // hiba esetén nem vész el a szöveg
                ShowStatus(ex.Message, error: true);
            }
            finally
            {
                busy = false;
            }
        }

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
                string caption = composer.Value.Trim();
                var message = await ApiClient.UploadAsync<MessageDto>($"/api/conversations/{open.Id}/attachments", filePath, caption);
                if (IsDisposed) return;

                if (current?.Id == open.Id)
                {
                    composer.SetValue("");
                    AppendMessage(message);
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
            if (open == null || open.UnreadCount == 0) return;

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

        // ------------------------------------------------------------
        // Valós idejű események
        // ------------------------------------------------------------

        // A beszélgetések listáját és az olvasatlan számlálót a ChatState frissíti (a főablak hívja);
        // itt csak a megnyitott beszélgetés üzenetei változnak.
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
                        if (current?.Id == e.ConversationId) ApplyRetracted(e.MessageId);
                        break;

                    case "message_edited":
                        // az esemény nem hozza az új szöveget, ezért a megnyitott beszélgetést újratöltjük
                        if (selfEdits.Remove(e.MessageId, out long editedAt) && Environment.TickCount64 - editedAt < 10_000)
                            break; // a saját szerkesztésünk visszhangja: már helyben alkalmaztuk

                        if (current?.Id == e.ConversationId)
                        {
                            bool stick = messageScroll.IsNearEnd();
                            await ReloadMessagesAsync(stick);
                            if (!IsDisposed && messages.Count > 0)
                                RefreshPreviewIfLast(messages[^1]);
                        }
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
            if (current?.Id != message.ConversationId) return;

            AppendMessage(message);

            // csak akkor olvasott, ha a beszélgetés tényleg látszik (keskeny ablaknál a lista mögött nem)
            if (ThreadVisible)
                await ApiClient.PostAsync($"/api/conversations/{message.ConversationId}/read");
        }

        /// <summary>Frissíti az olvasottsági jelzést (✓ → ✓✓) a saját, még olvasatlan üzeneteknél.</summary>
        private async Task RefreshReceiptsAsync()
        {
            var open = current;
            if (open == null || !ThreadVisible || receiptsBusy || IsDisposed || Form.ActiveForm == null) return;
            if (!messages.Any(m => m.SenderId == me.Id && !m.IsRead && !m.IsRetracted)) return;

            receiptsBusy = true;
            try
            {
                var fresh = await ApiClient.GetAsync<List<MessageDto>>($"/api/conversations/{open.Id}/messages");
                if (IsDisposed || current?.Id != open.Id) return;

                var readIds = fresh.Where(m => m.IsRead).Select(m => m.Id).ToHashSet();
                foreach (var local in messages.Where(m => m.SenderId == me.Id && !m.IsRead && readIds.Contains(m.Id)).ToList())
                {
                    local.IsReadValue = 1;
                    ReplaceBubble(local);
                }
            }
            catch (ApiException)
            {
                // nem kritikus: a következő ellenőrzéskor újra próbálkozik
            }
            finally
            {
                receiptsBusy = false;
            }
        }

        private void SaveDraft()
        {
            if (current == null) return;
            string text = composer.Value;
            if (text.Length == 0) Drafts.Remove(current.Id);
            else Drafts[current.Id] = text;
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
                ChatState.Changed -= OnChatStateChanged;
                ChatState.OpenConversationId = null;
                statusTimer.Dispose();
                receiptTimer.Dispose();
                filterDebounce.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

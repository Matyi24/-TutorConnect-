namespace TutorConnectApp
{
    // ================================================================
    // CHAT: kétpaneles felület (beszélgetések + üzenetek), valós idejű frissítéssel.
    // A osztály több fájlra oszlik:
    //   Chat.cs          – váz: vezérlők, elrendezés, megnyitott beszélgetés állapota
    //   Chat.List.cs     – beszélgetéslista és keresés az üzenetek között
    //   Chat.Messages.cs – üzenetek megjelenítése, görgetés, frissítés
    //   Chat.Actions.cs  – küldés, csatolmány, szerkesztés, visszavonás, profilok
    //   Chat.Events.cs   – valós idejű események, olvasottság, piszkozat, állapotsor
    // ================================================================
    internal sealed partial class ChatPage : PageBase
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
        private readonly System.Windows.Forms.Timer receiptTimer = new() { Interval = ReceiptBaseMs };

        /// <summary>Az el nem küldött üzenetek beszélgetésenként: oldalváltás vagy témaváltás után sem vesznek el.</summary>
        private static readonly Dictionary<int, string> Drafts = new();

        /// <summary>Kijelentkezéskor: a következő felhasználónak ne maradjanak meg az el nem küldött szövegek.</summary>
        public static void ClearDrafts() => Drafts.Clear();

        private static readonly Font EmojiButtonFont = new("Segoe UI Emoji", 14F, FontStyle.Regular, GraphicsUnit.Point);

        /// <summary>Üzenetek száma, amennyit egyszerre megjelenítünk (a régebbiek gombra töltődnek be).</summary>
        private const int MessagePageSize = 60;

        private int? restoreConversationId;
        private ConversationDto? current;
        private bool showThread;
        private bool busy;           // fájlfeltöltés folyamatban
        private long lastTypingSent;
        private bool restoringDraft;

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

            // keresés a beszélgetések és az üzenetek között (ékezetek nélkül is);
            // a lista a gépelés szünetében frissül, nem minden betűnél
            filterDebounce = new Debouncer(150, OnFilterChanged);
            listSearch = new SearchBox("Keresés a beszélgetések és üzenetek között...") { Margin = Ui.Pad(0) };
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
            backButton.Text = "\uE72B";
            backButton.AccessibleName = "Vissza a beszélgetésekhez";
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
            attachButton.Text = "\uE723";
            attachButton.AccessibleName = "Fájl csatolása";
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
            composer.PreferredHeightChanged += (_, _) => OnComposerHeightChanged();
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

        private void HandleDragDrop(object? sender, DragEventArgs e)
        {
            if (current == null || e.Data?.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files) return;

            // a megerősítő ablak ne a húzás (OLE) hívásán belül fusson, különben a fájlkezelő megfagy, amíg válaszolsz
            string path = files[0];
            BeginInvoke(new Action(async () => await UploadFileAsync(path, confirm: true)));
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
        private bool ThreadVisible => IsActive && ChatState.WindowVisible && current != null && (Wide || showThread);

        public override bool HasPendingRequest => PendingConversationId != null;
        public override bool SelfRefreshing => true; // a valós idejű események naprakészen tartják

        protected override void OnActiveChanged(bool active) => SyncOpenState();

        /// <summary>Visszatéréskor a közben érkezett üzenetek és az olvasottság rendbe kerül.</summary>
        protected override void OnRevisited()
        {
            if (current != null) _ = ResyncAsync();
        }

        private async Task ResyncAsync()
        {
            var open = current;
            if (open == null) return;

            await FetchMessagesAsync(open, firstLoad: false, scrollToEnd: false);
            if (!IsDisposed && ThreadVisible) await MarkReadAsync();
        }

        private void SyncOpenState()
        {
            if (ThreadVisible)
            {
                ChatState.OpenConversationId = current!.Id;
                ChatState.OpenOwner = this;
            }
            else if (ReferenceEquals(ChatState.OpenOwner, this))
            {
                ChatState.OpenConversationId = null;
                ChatState.OpenOwner = null;
            }
        }

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

        /// <summary>Az író sáv magassága a szöveg sorainak számát követi (a gombok az alsó sorhoz igazodnak).</summary>
        private void LayoutComposer()
        {
            int pad = Ui.S(16);
            int gap = Ui.S(10);
            int h = Ui.S(50);

            int barHeight = Math.Max(Ui.S(80), composer.PreferredHeight + Ui.S(30));
            if (composerBar.Height != barHeight)
                composerBar.Height = barHeight; // az átméretezés újra ide fut, de már azonos magassággal (nincs végtelen ciklus)

            int y = composerBar.Height - Ui.S(15) - h;
            attachButton.Location = new Point(pad, y);
            emojiButton.Location = new Point(attachButton.Right + gap, y);
            sendButton.Location = new Point(composerBar.Width - pad - sendButton.Width, y);
            int left = emojiButton.Right + gap;
            composer.SetBounds(left, Ui.S(15), Math.Max(sendButton.Left - gap - left, Ui.S(80)), composerBar.Height - Ui.S(30));
        }

        private void OnComposerHeightChanged()
        {
            if (IsDisposed) return;

            bool stick = messageScroll.IsNearEnd();
            LayoutComposer();
            if (stick) ScrollToEnd(animate: false); // az üzenetek ne csússzanak el a magasabb sáv alatt
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (ApiClient.CurrentUser != null)
                    StoreMessages(); // újraépítés (pl. témaváltás) után az üzenetek azonnal újra látszanak
                Realtime.EventReceived -= OnRealtime;
                ChatState.Changed -= OnChatStateChanged;
                if (ReferenceEquals(ChatState.OpenOwner, this))
                {
                    ChatState.OpenConversationId = null;
                    ChatState.OpenOwner = null;
                }
                statusTimer.Dispose();
                receiptTimer.Dispose();
                filterDebounce.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

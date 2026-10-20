namespace TutorConnectApp
{
    public partial class TutorMain : Form
    {

        private readonly Dictionary<string, Func<PageBase>> pages = new();
        private readonly List<string> pageOrder = new();
        private readonly Dictionary<string, string> pageTitles = new();
        private readonly bool startInTray;
        private bool quitting;
        private bool trayHintShown;
        private Icon? badgedIcon;
        private PageBase? currentPage;

        /// <summary>A bejelentkezett felhasználó aktuális adatai (a profil mentése után változhatnak).</summary>
        private static UserInfo Me => ApiClient.CurrentUser ?? throw new InvalidOperationException("Nincs bejelentkezett felhasználó.");
        private string currentKey = "";
        private readonly System.Windows.Forms.Timer badgeTimer = new() { Interval = 400 };
        private readonly System.Windows.Forms.Timer favoriteTimer = new() { Interval = 5 * 60 * 1000 };
        private bool favoriteChecking;
        private readonly LessonReminders reminders = new();
        private readonly System.Windows.Forms.Timer reminderTimer = new() { Interval = 30_000 };
        private bool reminderChecking;
        private readonly NotifyIcon tray = new();
        private ContextMenuStrip? trayMenu;
        private Icon? trayIcon;
        private string trayTarget = "";
        private int? trayConversationId;
        private long lastBookingToast;
        private FlowLayoutPanel? verifyBanner;
        private bool verifyChecking;

        /// <summary>Igaz, ha a felhasználó kijelentkezett (és nem simán bezárta az appot).</summary>
        public bool LoggedOut { get; private set; }

        /// <param name="startInTray">Igaz, ha az app a Windows indításakor (--tray) a háttérben indul.</param>
        public TutorMain(bool startInTray = false)
        {
            InitializeComponent();
            this.startInTray = startInTray;

            var user = ApiClient.CurrentUser ?? throw new InvalidOperationException("Nincs bejelentkezett felhasználó.");

            MinimumSize = Ui.Sz(480, 360);
            Text = $"TutorConnect – {user.Name}";
            RestoreWindowBounds();
            if (startInTray) WindowState = FormWindowState.Minimized;

            BuildMenu(user);

            navBar.SetUser(user.Name, user.Role);
            navBar.ItemClicked += NavBar_ItemClicked;
            ClientSizeChanged += (_, _) => navBar.ApplyResponsive(ClientSize.Width);
            navBar.ApplyResponsive(ClientSize.Width);

            BuildTray();

            Theme.Changed += OnThemeChanged;
            ApiClient.CurrentUserChanged += OnUserChanged;
            ApiClient.SessionExpired += OnSessionExpired;
            Realtime.EventReceived += OnRealtimeEvent;
            Realtime.ConnectionChanged += OnConnectionChanged;
            ChatState.Changed += UpdateChatBadge;
            AppEvents.BadgesStale += ScheduleBadges;
            badgeTimer.Tick += async (_, _) =>
            {
                badgeTimer.Stop();
                await RefreshBadgesAsync();
            };
            // a diák kedvenc oktatóinak új szabad időpontjairól 5 percenként érdeklődünk (amíg az app fut)
            if (user.Role == "STUDENT")
            {
                favoriteTimer.Tick += async (_, _) => await CheckFavoritesAsync();
                favoriteTimer.Start();
            }

            // emlékeztető a visszaigazolt órákra (1 órával és 10 perccel előtte): fél percenként ellenőrizzük
            if (user.Role is "STUDENT" or "TUTOR")
            {
                reminderTimer.Tick += async (_, _) => await CheckRemindersAsync();
                reminderTimer.Start();
            }

            BuildBanner();
            ApplyTheme();

            navBar.Select("home");
        }

        /// <summary>
        /// A menü szerepkör szerint épül fel, a weboldalhoz hasonlóan:
        /// mindenki: Főoldal, Chat, Tantárgyak, Profil, Beállítások; diák: + Oktatók, Naptár, Foglalás; oktató: + Naptár, Foglalás.
        /// Új menüpont: egy sor itt + egy oldal (PageBase utód).
        /// Ikonok: Segoe MDL2 Assets karakterek.
        /// </summary>
        private void BuildMenu(UserInfo user)
        {
            AddPage("home", "Főoldal", "", () => new HomePage(Me));
            AddPage("chat", "Chat", "", () => new ChatPage(Me));
            AddPage("subjects", "Tantárgyak", "", () => new SubjectsPage(Me));

            if (user.Role == "STUDENT")
                AddPage("tutors", "Oktatók", "", () => new TutorsPage());

            if (user.Role == "STUDENT" || user.Role == "TUTOR")
            {
                AddPage("calendar", "Naptár", "\uE787", () => new CalendarPage(Me));
                AddPage("bookings", "Foglalás", "\uE81C", () => new BookingsPage(Me));
            }

            AddPage("profile", "Profil", "", () => new ProfilePage());
            AddPage("settings", "Beállítások", "", () => new SettingsPage(Me));

            // művelet: nem kijelölhető gomb (a kijelentkezés a Profil oldalon van)
            navBar.AddAction("theme", "Sötét mód", "");
        }

        private void AddPage(string key, string text, string icon, Func<PageBase> factory)
        {
            pages[key] = factory;
            pageOrder.Add(key);
            pageTitles[key] = text;
            navBar.AddItem(key, text, icon);
        }

        /// <summary>
        /// Másik DPI-jű monitorra húzáskor a méretek az új nagyításhoz igazodnak, és az oldal újraépül.
        /// </summary>
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);

            Ui.SetDpi(e.DeviceDpiNew);
            MinimumSize = Ui.Sz(480, 360);
            navBar.ApplyScale();
            navBar.ApplyResponsive(ClientSize.Width);

            BuildBanner();
            if (pages.TryGetValue(currentKey, out var factory))
                ShowPage(factory(), carryState: true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyBlack(this); // fekete címsor és szegély
        }

        // ------------------------------------------------------------
        // Billentyűparancsok: Ctrl+1..9 oldalváltás, Ctrl+K gyorskereső
        // ------------------------------------------------------------

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            Realtime.Start();   // valós idejű értesítések (új üzenet, foglalás-változás)
            _ = LoadChatAsync(); // a navbár olvasatlan-jelvényéhez
            ScheduleBadges();
            if (Me.Role == "STUDENT") _ = CheckFavoritesAsync(); // az első ellenőrzés csak alapállapotot rögzít
            _ = CheckRemindersAsync();

            // Windows-indításkor a tálcára kerül (ha a bezárás is tálcára kicsinyít, el is tűnik a tálcáról)
            if (startInTray && AppSettings.MinimizeToTray) Hide();
        }

        /// <summary>Az ablak pozíciója és mérete a következő induláshoz.</summary>
        private void SaveWindowBounds()
        {
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            AppSettings.SaveWindow(bounds, WindowState == FormWindowState.Maximized);
        }

        private void RestoreWindowBounds()
        {
            if (AppSettings.LoadWindow() is not { } saved) return;

            // csak akkor, ha az ablak ma is látszik valamelyik képernyőn (pl. levált monitor után ne maradjon a semmiben)
            bool visible = Screen.AllScreens.Any(screen =>
            {
                var overlap = Rectangle.Intersect(screen.WorkingArea, saved.Bounds);
                return overlap.Width > 120 && overlap.Height > 80;
            });
            if (!visible) return;

            StartPosition = FormStartPosition.Manual;
            Bounds = saved.Bounds;
            if (saved.Maximized) WindowState = FormWindowState.Maximized;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // a bezárás tálcára kicsinyít (ha be van kapcsolva); a kilépés a tálcaikon menüjéből vagy kijelentkezéskor megy
            if (!LoggedOut && !quitting && AppSettings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                SaveWindowBounds();
                Hide();

                if (!trayHintShown)
                {
                    trayHintShown = true;
                    tray.ShowBalloonTip(4000, "TutorConnect", "Az app a tálcán fut tovább. A kilépéshez használd a tálcaikon menüjét.", ToolTipIcon.Info);
                }
            }
            else
            {
                SaveWindowBounds();
            }

            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Theme.Changed -= OnThemeChanged;
            ApiClient.CurrentUserChanged -= OnUserChanged;
            ApiClient.SessionExpired -= OnSessionExpired;
            Realtime.EventReceived -= OnRealtimeEvent;
            Realtime.ConnectionChanged -= OnConnectionChanged;
            ChatState.Changed -= UpdateChatBadge;
            AppEvents.BadgesStale -= ScheduleBadges;
            badgeTimer.Dispose();
            favoriteTimer.Dispose();
            reminderTimer.Dispose();
            Realtime.Stop();
            ChatState.Clear(); // a következő bejelentkezőnek ne maradjon meg a beszélgetés-lista

            tray.Visible = false;
            tray.Dispose();
            trayMenu?.Dispose();
            trayIcon?.Dispose();
            badgedIcon?.Dispose();
            base.OnFormClosed(e);
        }

        // ------------------------------------------------------------
        // Jelvények: olvasatlan üzenetek és (oktatónál) függő foglalások
        // ------------------------------------------------------------

        private async void OnRealtimeEvent(RealtimeEvent e)
        {
            if (IsDisposed || ApiClient.CurrentUser == null) return; // kijelentkezés közben még érkezhet esemény

            try
            {
                switch (e.Type)
                {
                    case "new_message" when e.Message != null:
                        // előbb a közös állapot (olvasatlan számláló), utána az értesítés a már ismert névvel
                        await ChatState.OnNewMessageAsync(e.Message, Me.Id);
                        NotifyNewMessage(e.Message);
                        break;

                    case "message_retracted":
                    case "message_edited":
                        await ChatState.OnMessageChangedAsync(e.ConversationId);
                        break;

                    case "booking_update":
                        ScheduleBadges();
                        NotifyBookingChange();
                        break;
                }
            }
            catch (ApiException)
            {
                // a következő esemény vagy az újracsatlakozás rendbe teszi
            }
        }

        private void OnConnectionChanged(bool connected)
        {
            if (!connected || IsDisposed) return;

            // újracsatlakozás után lehet, hogy lemaradtunk eseményekről: a számlálókat újra lekérjük
            if (Environment.TickCount64 - ChatState.LastLoadedTick > 3000)
                _ = LoadChatAsync();
            ScheduleBadges();
        }

        private async Task LoadChatAsync()
        {
            try
            {
                await ChatState.LoadAsync();
            }
            catch (ApiException)
            {
                // a jelvény nem kritikus: az újracsatlakozás vagy a Chat oldal megnyitása újra próbálkozik
            }
        }

        private void UpdateChatBadge()
        {
            if (IsDisposed) return;

            int unread = ChatState.TotalUnread;
            navBar.SetBadge("chat", unread);
            UpdateTitle();
            UpdateTrayIcon(unread);
        }

        /// <summary>Az ablak címe: az olvasatlan üzenetek száma a taskbaron is látszik.</summary>
        private void UpdateTitle()
        {
            int unread = ChatState.TotalUnread;
            Text = (unread > 0 ? $"({unread}) " : "") + $"TutorConnect – {Me.Name}";
        }

        /// <summary>Rövid késleltetéssel frissíti a jelvényeket, így az egymás utáni események egyetlen lekérést adnak.</summary>
        private void ScheduleBadges()
        {
            if (IsDisposed) return;
            reminders.MarkStale(); // a foglalások változhattak: az emlékeztetők friss listából dolgozzanak
            badgeTimer.Stop();
            badgeTimer.Start();
        }

        /// <summary>A függő foglalások száma az oktatónál (kis, külön végpont; az olvasatlan üzeneteket a ChatState számolja).</summary>
        private async Task RefreshBadgesAsync()
        {
            if (Me.Role != "TUTOR") return;

            try
            {
                var summary = await ApiClient.GetAsync<BookingSummaryDto>("/api/bookings/summary");
                if (IsDisposed) return;
                navBar.SetBadge("bookings", summary.Pending);
            }
            catch (ApiException)
            {
                // a jelvény nem kritikus: a következő eseménynél újra próbálkozik
            }
        }

        // ------------------------------------------------------------
        // Tálcaikon és értesítések (csak akkor, ha az ablak nincs előtérben)
        // ------------------------------------------------------------

        private void NavBar_ItemClicked(object? sender, string key)
        {
            if (key == "theme")
            {
                Theme.SetDark(!Theme.DarkSetting);
                return;
            }

            if (pages.TryGetValue(key, out var factory))
            {
                currentKey = key;
                ShowPage(factory());
            }
        }

        /// <summary>A munkamenet lejárt (7 nap telt el, vagy a szerver újraindult): szólunk, és visszalépünk a belépéshez.</summary>
        private void OnSessionExpired()
        {
            if (IsDisposed || !IsHandleCreated) return;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || LoggedOut) return;

                MessageBox.Show(this, "A munkameneted lejárt, ezért újra be kell jelentkezned.", "Munkamenet lejárt",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                ApiClient.DropSession();
                LoggedOut = true;
                Close();
            }));
        }

        private async Task LogoutAsync()
        {
            await ApiClient.LogoutAsync();
            LoggedOut = true;
            Close();
        }

        private void OnUserChanged()
        {
            // a navbár felhasználói blokkja és az ablak címe az új névvel
            navBar.SetUser(Me.Name, Me.Role);
            UpdateTitle();
            if (verifyBanner != null) verifyBanner.Visible = !Me.EmailVerified;
        }

        // ------------------------------------------------------------
        // E-mail megerősítés: a szerver foglaláshoz és időpont-hirdetéshez megköveteli
        // ------------------------------------------------------------

        private void OnThemeChanged()
        {
            ApplyTheme();

            // az oldal újraépül az új színekkel
            if (pages.TryGetValue(currentKey, out var factory))
                ShowPage(factory(), carryState: true);
        }

        /// <summary>A főablak keretének színei a téma szerint (az oldalak maguktól újraépülnek).</summary>
        private void ApplyTheme()
        {
            BackColor = Theme.PageBg;
            contentPanel.BackColor = Theme.PageBg;
            navBar.ApplyTheme();

            // a téma-váltó gomb a következő állapotot mutatja
            if (Theme.DarkSetting)
                navBar.SetItemContent("theme", "Világos mód", "");
            else
                navBar.SetItemContent("theme", "Sötét mód", "");
        }

        /// <param name="carryState">Igaz, ha ugyanazt az oldalt építjük újra (téma-, DPI-váltás): a felhasználó beállításai átkerülnek.</param>
        private void ShowPage(PageBase page, bool carryState = false)
        {
            if (carryState && currentPage != null)
                page.RestoreState(currentPage.CaptureState());

            page.NavigateRequested += key => navBar.Select(key);
            page.LogoutRequested += () => _ = LogoutAsync();

            contentPanel.SuspendLayout();

            var old = currentPage;
            currentPage = page;
            contentPanel.Controls.Add(page);
            page.BringToFront();
            if (old != null)
            {
                contentPanel.Controls.Remove(old);
                old.Dispose();
            }

            contentPanel.ResumeLayout(true);
            _ = page.LoadAsync();
        }
    }
}

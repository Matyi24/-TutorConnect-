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
        private readonly Dictionary<string, PageBase> cache = new(); // a már felépített oldalak: váltáskor nem épülnek újra
        private PageBase? currentPage;       // a látható oldal
        private PageBase? incomingPage;      // a háttérben, a látható oldal mögött épülő vagy frissülő oldal
        private PageBase? activePage;        // az oldal, amelyre a felhasználó navigált
        private readonly System.Windows.Forms.Timer swapTimer = new() { Interval = 600 };
        private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 180 };

        /// <summary>A bejelentkezett felhasználó aktuális adatai (a profil mentése után változhatnak).</summary>
        private static UserInfo Me => ApiClient.CurrentUser ?? throw new InvalidOperationException("Nincs bejelentkezett felhasználó.");
        private string currentKey = "";
        private readonly System.Windows.Forms.Timer badgeTimer = new() { Interval = 400 };
        private readonly System.Windows.Forms.Timer favoriteTimer = new() { Interval = 5 * 60 * 1000 };
        private bool favoriteChecking;
        private readonly LessonReminders reminders = new();
        private readonly System.Windows.Forms.Timer reminderTimer = new() { Interval = 30_000 };
        private bool reminderChecking;
        private long lastReviewCheck;
        private bool themePending;
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
            Resize += (_, _) => UpdateWindowVisibility();
            VisibleChanged += (_, _) => UpdateWindowVisibility();
            navBar.ApplyResponsive(ClientSize.Width);

            BuildTray();

            swapTimer.Tick += (_, _) =>
            {
                swapTimer.Stop();
                if (incomingPage != null) CompleteSwap(incomingPage);
            };
            refreshTimer.Tick += (_, _) =>
            {
                refreshTimer.Stop();
                RefreshCurrentIfStale();
            };

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

            navBar.Select(pageOrder[0]); // adminnál az Áttekintés, másoknál a Főoldal
        }

        /// <summary>
        /// A menü szerepkör szerint épül fel, a weboldalhoz hasonlóan:
        /// mindenki: Főoldal, Chat, Tantárgyak, Profil, Beállítások; diák: + Oktatók, Naptár, Foglalás; oktató: + Naptár, Foglalás.
        /// Új menüpont: egy sor itt + egy oldal (PageBase utód).
        /// Ikonok: Segoe MDL2 Assets karakterek.
        /// </summary>
        private void BuildMenu(UserInfo user)
        {
            // az adminisztrátor saját menüt kap (mint a webes admin felület fülei); chatje és foglalása nincs
            if (user.Role == "ADMIN")
            {
                AddPage("admin-overview", "Áttekintés", "\uE80F", () => new AdminOverviewPage());
                AddPage("admin-reports", "Jelentések", "\uE7BA", () => new AdminReportsPage());
                AddPage("admin-users", "Felhasználók", "\uE716", () => new AdminUsersPage());
                AddPage("admin-reviews", "Értékelések", "\uE734", () => new AdminReviewsPage());
                AddPage("admin-bookings", "Foglalások", "\uE787", () => new AdminBookingsPage());
                AddPage("profile", "Profil", "\uE77B", () => new ProfilePage());
                AddPage("settings", "Beállítások", "\uE713", () => new SettingsPage(Me));
                navBar.AddAction("theme", "Sötét mód", "\uE708");
                return;
            }

            AddPage("home", "Főoldal", "\uE80F", () => new HomePage(Me));
            AddPage("chat", "Chat", "\uE8BD", () => new ChatPage(Me));
            AddPage("subjects", "Tantárgyak", "\uE8F1", () => new SubjectsPage(Me));

            if (user.Role == "STUDENT")
                AddPage("tutors", "Oktatók", "\uE716", () => new TutorsPage());

            if (user.Role == "STUDENT" || user.Role == "TUTOR")
            {
                AddPage("calendar", "Naptár", "\uE787", () => new CalendarPage(Me));
                AddPage("bookings", "Foglalás", "\uE81C", () => new BookingsPage(Me));
                AddPage("stats", "Statisztika", "\uE9D2", () => new StatsPage(Me));
            }

            AddPage("profile", "Profil", "\uE77B", () => new ProfilePage());
            AddPage("settings", "Beállítások", "\uE713", () => new SettingsPage(Me));

            // művelet: nem kijelölhető gomb (a kijelentkezés a Profil oldalon van)
            navBar.AddAction("theme", "Sötét mód", "\uE708");
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
            RebuildAllPages();
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
            swapTimer.Dispose();
            refreshTimer.Dispose();
            favoriteTimer.Dispose();
            reminderTimer.Dispose();
            Realtime.Stop();
            ChatState.Clear(); // a következő bejelentkezőnek ne maradjon meg a beszélgetés-lista
            ChatPage.ClearDrafts();

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
                        ScheduleRefresh(); // a látható oldal (pl. Foglalások) rövid késéssel frissül
                        NotifyBookingChange();
                        break;
                }
            }
            catch (ApiException)
            {
                // a következő esemény vagy az újracsatlakozás rendbe teszi
            }
        }

        /// <summary>Lekicsinyítve vagy tálcára rejtve a beérkező üzenetek olvasatlanok maradnak; visszaállításkor a megnyitott beszélgetés olvasottá válik.</summary>
        private void UpdateWindowVisibility()
        {
            bool visible = Visible && WindowState != FormWindowState.Minimized;
            if (ChatState.WindowVisible == visible) return;

            ChatState.WindowVisible = visible;
            if (visible) currentPage?.Revisit();
        }

        private void OnConnectionChanged(bool connected)
        {
            if (!connected || IsDisposed) return;

            // az összeköttetés megszakadása alatt lemaradhattunk üzenetekről: a tárolt üzenetek elavultak, a megnyitott beszélgetés frissül
            ChatMessages.Clear();
            currentPage?.Revisit();

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
            MarkPagesStale(); // a foglalások változhattak: a háttérben álló oldalak a következő megnyitáskor frissülnek
            reminders.MarkStale(); // a foglalások változhattak: az emlékeztetők friss listából dolgozzanak
            badgeTimer.Stop();
            badgeTimer.Start();
        }

        /// <summary>A függő foglalások száma az oktatónál (kis, külön végpont; az olvasatlan üzeneteket a ChatState számolja).</summary>
        private async Task RefreshBadgesAsync()
        {
            if (Me.Role == "ADMIN")
            {
                // adminnál a nyitott jelentések száma jelenik meg a Jelentések menüponton
                try
                {
                    var stats = await ApiClient.GetAsync<AdminStatsDto>("/api/admin/stats");
                    if (!IsDisposed) navBar.SetBadge("admin-reports", stats.Reports.Open);
                }
                catch (ApiException)
                {
                    // a jelvény nem kritikus
                }
                return;
            }

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

            if (!pages.ContainsKey(key)) return;

            // ugyanarra a menüpontra újra kattintva az oldal frissül
            if (key == currentKey && currentPage != null && cache.TryGetValue(key, out var same) && same == currentPage)
                same.MarkStale();

            currentKey = key;
            ShowKey(key);
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
            MarkPagesStale();
            if (verifyBanner != null) verifyBanner.Visible = !Me.EmailVerified;
        }

        // ------------------------------------------------------------
        // E-mail megerősítés: a szerver foglaláshoz és időpont-hirdetéshez megköveteli
        // ------------------------------------------------------------

        private void OnThemeChanged()
        {
            // az oldalak az új színekkel épülnek újra; a keret (menü, háttér) az új oldallal egyszerre vált,
            // így nincs olyan pillanat, amikor új színű menü mellett még a régi színű oldal látszik
            themePending = true;
            RebuildAllPages();

            if (themePending && incomingPage == null) ApplyThemeNow(); // nincs mire várni (pl. ismeretlen oldal)
        }

        /// <summary>A keret színeit egyetlen lépésben, rajzolás-szünettel váltja.</summary>
        private void ApplyThemeNow()
        {
            themePending = false;
            Redraw.Suspend(this);
            try { ApplyTheme(); }
            finally { Redraw.Resume(this); }
        }

        /// <summary>A főablak keretének színei a téma szerint (az oldalak maguktól újraépülnek).</summary>
        private void ApplyTheme()
        {
            BackColor = Theme.PageBg;
            contentPanel.BackColor = Theme.PageBg;
            navBar.ApplyTheme();

            // a téma-váltó gomb a következő állapotot mutatja
            if (Theme.DarkSetting)
                navBar.SetItemContent("theme", "Világos mód", "\uE706");
            else
                navBar.SetItemContent("theme", "Sötét mód", "\uE708");
        }

        // ------------------------------------------------------------
        // Oldalkezelés: az oldalak a főablakban maradnak (gyorsítótár), váltáskor csak a láthatóság és a sorrend változik.
        // Így gyors váltogatásnál sincs építés, bontás és újrarajzolás, ami lassítaná vagy villogtatná a felületet.
        // ------------------------------------------------------------

        private void ShowKey(string key)
        {
            if (!pages.TryGetValue(key, out var factory)) return;

            if (cache.TryGetValue(key, out var page) && page.IsDisposed)
            {
                cache.Remove(key);
                page = null;
            }

            if (page == null)
            {
                page = factory();
                AttachPage(key, page);
            }

            ActivatePage(page);
            PresentPage(page);
        }

        /// <summary>Új oldal felvétele: a látható oldal mögé kerül, és a gyorsítótárba.</summary>
        private void AttachPage(string key, PageBase page)
        {
            page.NavigateRequested += navKey => navBar.Select(navKey);
            page.LogoutRequested += () => _ = LogoutAsync();
            page.Ready += () =>
            {
                if (page == incomingPage) CompleteSwap(page);
            };

            cache[key] = page;

            contentPanel.SuspendLayout();
            contentPanel.Controls.Add(page);
            if (currentPage != null)
                contentPanel.Controls.SetChildIndex(page, contentPanel.Controls.GetChildIndex(currentPage) + 1);
            else
                page.BringToFront();
            contentPanel.ResumeLayout(true);
        }

        private void ActivatePage(PageBase page)
        {
            var previous = activePage;
            activePage = page;
            if (previous == page) return;

            if (previous != null && !previous.IsDisposed)
            {
                previous.SetActive(false);

                // amit ott a felhasználó megváltoztatott, azt a többi oldal a következő megnyitáskor újratölti
                if (previous.InvalidatesOthers)
                    foreach (var other in cache.Values)
                        if (other != previous) other.MarkStale();
            }
            page.SetActive(true);
        }

        private void PresentPage(PageBase page)
        {
            // más oldal még épülő példánya már nem kell a háttérben (a kész része megmarad a gyorsítótárban)
            if (incomingPage != null && incomingPage != page)
            {
                incomingPage.Visible = false;
                incomingPage = null;
            }

            if (page == currentPage)
            {
                incomingPage = null;
                swapTimer.Stop();
                if (page.HasPendingRequest) _ = page.ReloadAsync();
                else ScheduleRefresh();
                return;
            }

            // az első oldal azonnal látszik
            if (currentPage == null)
            {
                SwapTo(page);
                _ = page.LoadAsync();
                return;
            }

            // kész oldal: azonnali váltás, a friss adatok szükség esetén utólag, a helyén érkeznek
            if (page.Loaded && !page.HasPendingRequest)
            {
                incomingPage = null;
                swapTimer.Stop();
                SwapTo(page);
                page.Revisit();
                ScheduleRefresh();
                return;
            }

            // még nem kész (első megnyitás, vagy másik oldal kérésére újra kell építeni): a látható oldal MÖGÖTT épül fel,
            // és amint kész, egyben váltunk rá; így nincs üres vagy félig felépült kép, és nem villan fel a betöltésjelző
            incomingPage = page;
            page.Visible = true;
            swapTimer.Stop();
            swapTimer.Start(); // lassú betöltésnél az új oldal betöltésjelzővel látszik, nem várakoztatunk örökké
            _ = page.Loaded ? page.ReloadAsync() : page.LoadAsync();
        }

        private void CompleteSwap(PageBase page)
        {
            if (IsDisposed || page != incomingPage) return;

            swapTimer.Stop();
            incomingPage = null;
            SwapTo(page);
        }

        /// <summary>A kész oldal előre kerül, az előző eltűnik (a gyorsítótárban marad, vagy megszűnik, ha már nincs benne).</summary>
        private void SwapTo(PageBase page)
        {
            var old = currentPage;
            currentPage = page;

            bool restyle = themePending; // témaváltás: a keret az oldalcserével egy lépésben vált
            themePending = false;
            if (restyle) Redraw.Suspend(this);

            contentPanel.SuspendLayout();
            page.Visible = true;
            page.BringToFront();
            if (old != null && old != page)
            {
                old.Visible = false;
                if (!cache.ContainsValue(old))
                {
                    contentPanel.Controls.Remove(old);
                    if (IsHandleCreated) BeginInvoke(new Action(old.Dispose)); // a bontás a rajzolástól külön fusson
                    else old.Dispose();
                }
            }
            if (restyle) ApplyTheme();
            contentPanel.ResumeLayout(true);

            if (restyle) Redraw.Resume(this);
            else Redraw.PaintNow(page); // az oldal egyetlen menetben festődik ki, nem darabokban
        }

        private void ScheduleRefresh()
        {
            refreshTimer.Stop();
            refreshTimer.Start(); // gyors váltogatásnál csak az az oldal frissül, amelynél a felhasználó megállt
        }

        private void RefreshCurrentIfStale()
        {
            var page = currentPage;
            if (IsDisposed || page == null || page != activePage || page.IsDisposed) return;
            if (page.Loaded && !page.Loading && page.IsStale)
                _ = page.ReloadAsync();
        }

        private void MarkPagesStale()
        {
            foreach (var page in cache.Values)
                page.MarkStale();
        }

        /// <summary>Téma- vagy DPI-váltás után minden oldal újraépül; az éppen látható oldal beállításai megmaradnak.</summary>
        private void RebuildAllPages()
        {
            var state = currentPage?.CaptureState();

            incomingPage = null;
            swapTimer.Stop();
            refreshTimer.Stop();

            foreach (var page in cache.Values.Where(p => p != currentPage).ToList())
            {
                contentPanel.Controls.Remove(page);
                page.Dispose();
            }
            cache.Clear();
            activePage = null;

            if (!pages.TryGetValue(currentKey, out var factory)) return;

            // a régi oldal addig látszik, amíg az új, új színű változata el nem készül
            var fresh = factory();
            if (state != null) fresh.RestoreState(state);
            AttachPage(currentKey, fresh);
            ActivatePage(fresh);

            if (currentPage == null)
            {
                SwapTo(fresh);
                _ = fresh.LoadAsync();
                return;
            }

            incomingPage = fresh;
            swapTimer.Start();
            _ = fresh.LoadAsync();
        }
    }
}

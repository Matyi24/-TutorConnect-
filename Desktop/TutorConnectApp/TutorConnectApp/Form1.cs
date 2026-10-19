using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace TutorConnectApp
{
    public partial class TutorMain : Form
    {
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

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

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.K))
            {
                _ = OpenQuickSwitcherAsync();
                return true;
            }

            for (int i = 0; i < pageOrder.Count && i < 9; i++)
            {
                if (keyData == (Keys.Control | (Keys.D1 + i)) || keyData == (Keys.Control | (Keys.NumPad1 + i)))
                {
                    navBar.Select(pageOrder[i]);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private async Task OpenQuickSwitcherAsync()
        {
            if (IsDisposed || ActiveForm != this) return; // másik ablak (dialógus) fölött nem nyílik

            var entries = new List<QuickEntry>();
            for (int i = 0; i < pageOrder.Count; i++)
            {
                string key = pageOrder[i];
                entries.Add(new QuickEntry("page", key, 0, pageTitles[key], i < 9 ? $"Oldal · Ctrl+{i + 1}" : "Oldal", "→"));
            }
            entries.Add(new QuickEntry("action", "theme", 0, "Téma váltása", "Sötét / világos mód", "◐"));
            entries.Add(new QuickEntry("action", "logout", 0, "Kijelentkezés", "Vissza a belépőhöz", "↩"));

            using var dialog = new QuickSwitcherDialog(entries);
            if (Me.Role == "STUDENT") _ = AddTutorsToSwitcherAsync(dialog);

            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Chosen is not { } chosen || IsDisposed) return;

            switch (chosen.Kind)
            {
                case "page":
                    navBar.Select(chosen.Key);
                    break;
                case "action" when chosen.Key == "theme":
                    Theme.SetDark(!Theme.DarkSetting);
                    break;
                case "action" when chosen.Key == "logout":
                    _ = LogoutAsync();
                    break;
                case "chat":
                    ChatPage.PendingConversationId = chosen.Id;
                    navBar.Select("chat");
                    break;
                case "tutor":
                    await ShowTutorAsync(chosen.Id);
                    break;
            }
        }

        private static async Task AddTutorsToSwitcherAsync(QuickSwitcherDialog dialog)
        {
            try
            {
                var tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                if (dialog.IsDisposed) return;

                dialog.AddEntries(tutors.Select(t =>
                    new QuickEntry("tutor", "tutor", t.Id, t.FullName, "Oktató · " + (string.IsNullOrWhiteSpace(t.Subjects) ? "nincs tantárgy" : t.Subjects), "★")));
            }
            catch (ApiException)
            {
                // az oktatók nélkül is használható a gyorskereső
            }
        }

        private async Task ShowTutorAsync(int tutorId)
        {
            try
            {
                var tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                if (tutors.FirstOrDefault(t => t.Id == tutorId) is { } tutor)
                    await TutorActions.ShowProfileAsync(this, tutor, key => navBar.Select(key));
            }
            catch (ApiException ex)
            {
                MessageBox.Show(this, ex.Message, "Oktató profilja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

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
            if (IsDisposed) return;

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

        /// <summary>A tálcaikon piros jelvényt kap az olvasatlan üzenetek számával, a súgószövege is mutatja.</summary>
        private void UpdateTrayIcon(int unread)
        {
            var previous = badgedIcon;
            badgedIcon = null;

            if (unread > 0)
            {
                using var bitmap = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.DrawIcon(trayIcon ?? SystemIcons.Application, new Rectangle(0, 0, 32, 32));

                    var dot = new Rectangle(12, 12, 20, 20);
                    using (var red = new SolidBrush(Theme.Danger))
                        g.FillEllipse(red, dot);
                    using var font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var white = new SolidBrush(Color.White);
                    using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(unread > 9 ? "9+" : unread.ToString(), font, white, dot, center);
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using var temp = Icon.FromHandle(handle);
                    badgedIcon = (Icon)temp.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }

            tray.Icon = badgedIcon ?? trayIcon ?? SystemIcons.Application;
            tray.Text = unread > 0 ? Shorten($"TutorConnect – {unread} olvasatlan üzenet", 60) : "TutorConnect";
            previous?.Dispose();
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

        private void BuildTray()
        {
            try
            {
                trayIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException)
            {
                trayIcon = null; // a rendszer alapértelmezett ikonja marad
            }

            tray.Icon = trayIcon ?? SystemIcons.Application;
            tray.Text = "TutorConnect";

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Megnyitás", null, (_, _) => RestoreFromTray("", null));
            trayMenu.Items.Add("Kilépés", null, (_, _) =>
            {
                quitting = true;
                Close();
            });
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += (_, _) => RestoreFromTray("", null);
            tray.BalloonTipClicked += (_, _) => RestoreFromTray(trayTarget, trayConversationId);
            tray.Visible = true;
        }

        /// <summary>Előhozza az ablakot, és (ha kell) a kért oldalra, illetve beszélgetésre ugrik.</summary>
        private void RestoreFromTray(string target, int? targetId)
        {
            if (IsDisposed) return;

            if (!Visible) Show(); // a tálcára rejtett ablak visszahozása
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();

            if (target.Length == 0) return;
            if (target == "chat" && targetId is int conversationId)
                ChatPage.PendingConversationId = conversationId;
            if (target == "calendar" && targetId is int tutorId)
                CalendarPage.InitialTutorId = tutorId;
            navBar.Select(target);
        }

        /// <summary>Igaz, ha az alkalmazás egyik ablaka sem aktív (háttérben van vagy le van kicsinyítve).</summary>
        private bool InBackground => WindowState == FormWindowState.Minimized || ActiveForm == null;

        /// <param name="always">Igaz: akkor is jelez, ha az ablak előtérben van (pl. kedvenc oktató új időpontja).</param>
        private void ShowToast(string title, string text, string target, int? conversationId = null, bool always = false)
        {
            if (IsDisposed || !AppSettings.NotificationsEnabled || (!always && !InBackground)) return;

            trayTarget = target;
            trayConversationId = conversationId;
            tray.ShowBalloonTip(6000, Shorten(title, 60), Shorten(text, 200), ToolTipIcon.Info);
        }

        private static string Shorten(string text, int max)
        {
            text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= max ? text : text[..(max - 1)] + "…";
        }

        private void NotifyNewMessage(MessageDto message)
        {
            if (message.SenderId == Me.Id) return;

            string name = ChatState.Find(message.ConversationId)?.OtherUserName ?? "";
            bool onlyFile = message.HasAttachment && (string.IsNullOrWhiteSpace(message.Content) || message.Content == message.AttachmentName);
            string body = onlyFile ? "Csatolmány: " + message.AttachmentName : message.Content;

            ShowToast(name.Length > 0 ? $"Új üzenet: {name}" : "Új üzenet", body, "chat", message.ConversationId);
        }

        /// <summary>
        /// A kedvenc oktatók szabad időpontjainak lekérése. Ha olyan időpont jelent meg, amit az előző ellenőrzéskor
        /// még nem láttunk, értesítést kapunk. Az első ellenőrzés csak az alapállapotot rögzíti.
        /// </summary>
        private async Task CheckFavoritesAsync()
        {
            if (favoriteChecking || IsDisposed || Me.Role != "STUDENT") return;

            var ids = Favorites.Ids.ToList();
            if (ids.Count == 0) return;

            favoriteChecking = true;
            try
            {
                foreach (int tutorId in ids)
                {
                    try
                    {
                        var data = await ApiClient.GetAsync<AvailabilityDto>($"/api/tutors/{tutorId}/availability");
                        if (IsDisposed) return;

                        var current = data.Slots.Select(s => s.Id).ToList();
                        var known = Favorites.GetKnown(tutorId);
                        Favorites.SetKnown(tutorId, current);

                        int added = known == null ? 0 : current.Count(id => !known.Contains(id));
                        if (added == 0 || !AppSettings.FavoriteAlertsEnabled) continue;

                        var tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                        string name = tutors.FirstOrDefault(t => t.Id == tutorId)?.FullName ?? "Egy kedvenc oktatód";
                        ShowToast("Új szabad időpont",
                            added == 1 ? $"{name} új szabad időpontot hirdetett." : $"{name} {added} új szabad időpontot hirdetett.",
                            "calendar", tutorId, always: true);
                    }
                    catch (ApiException)
                    {
                        // egy oktató lekérése nem sikerült (pl. törölt fiók): a többit tovább ellenőrizzük
                    }
                }
            }
            finally
            {
                favoriteChecking = false;
            }
        }

        /// <summary>
        /// Emlékeztető a közelgő órákra. A lista tíz percenként (és foglalás-változáskor) frissül, az értesítés
        /// az óra előtt 1 órával és 10 perccel jön, az ablak állapotától függetlenül.
        /// </summary>
        private async Task CheckRemindersAsync()
        {
            if (reminderChecking || IsDisposed || Me.Role is not ("STUDENT" or "TUTOR")) return;

            reminderChecking = true;
            try
            {
                if (reminders.NeedsRefresh)
                    await reminders.RefreshAsync();
                if (IsDisposed || !AppSettings.RemindersEnabled) return;

                foreach (var (booking, minutes, final) in reminders.Due())
                {
                    string who = (booking.AsStudent ? "Oktató: " : "Diák: ") + (booking.OtherName ?? "ismeretlen");
                    string title = final ? "Az órád hamarosan kezdődik" : "Az órád 1 órán belül kezdődik";
                    string text = $"{booking.SubjectName ?? "Óra"} · {who}\n{Ui.FormatRange(booking.StartTime, booking.EndTime)}  ({minutes} perc múlva)";
                    ShowToast(title, text, "bookings", always: true);
                }
            }
            catch (ApiException)
            {
                // nincs kapcsolat: a következő ellenőrzésnél újra próbálkozik
            }
            finally
            {
                reminderChecking = false;
            }
        }

        private void NotifyBookingChange()
        {
            // egymás utáni események (több foglalás) ne záporozzanak értesítésként
            long now = Environment.TickCount64;
            if (now - lastBookingToast < 10_000) return;
            lastBookingToast = now;

            ShowToast("Foglalás", "Változás történt a foglalásaidban.", "bookings");
        }

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

        /// <summary>Figyelmeztető sáv az ablak tetején, amíg az e-mail cím nincs megerősítve.</summary>
        private void BuildBanner()
        {
            if (verifyBanner != null)
            {
                contentPanel.Controls.Remove(verifyBanner);
                verifyBanner.Dispose();
            }

            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                BackColor = Theme.Warning,
                Padding = Ui.Pad(16, 10, 16, 6),
                Visible = !Me.EmailVerified
            };

            var text = Ui.Lbl("Az e-mail címed még nincs megerősítve, ezért nem tudsz foglalni, illetve időpontot hirdetni.", 10F, Theme.White, bold: true, margin: Ui.Pad(0, 6, 14, 4));
            bar.Controls.Add(text);
            bar.Controls.Add(Ui.Btn("Levél újraküldése", Theme.White, Color.Black, async (_, _) => await VerifyUi.ResendAsync(this)));
            bar.Controls.Add(Ui.Btn("Már megerősítettem", Theme.White, Color.Black, async (_, _) => await CheckVerifiedAsync(true)));

            verifyBanner = bar;
            contentPanel.Controls.Add(bar);
            // a Fill oldalnak a Z-sorrendben legelöl kell lennie, különben a sáv alá csúszik
            currentPage?.BringToFront();
        }

        /// <summary>Újra lekéri a fiók állapotát (pl. miután a böngészőben megnyitotta a linket).</summary>
        private async Task CheckVerifiedAsync(bool manual)
        {
            if (verifyChecking || IsDisposed) return;
            verifyChecking = true;
            try
            {
                await ApiClient.RefreshUserAsync();
                if (manual && !IsDisposed && !Me.EmailVerified)
                    MessageBox.Show(this, "A szerver szerint az e-mail címed még nincs megerősítve. Nyisd meg a levélben kapott linket, majd próbáld újra.",
                        "E-mail megerősítése", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (ApiException ex)
            {
                if (manual) MessageBox.Show(this, ex.Message, "Nem sikerült frissíteni", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                verifyChecking = false;
            }
        }

        /// <summary>Ha a felhasználó visszatér az ablakhoz (a böngészőből), csendben újraellenőrizzük.</summary>
        protected override async void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (ApiClient.CurrentUser is { EmailVerified: false })
                await CheckVerifiedAsync(false);
        }

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

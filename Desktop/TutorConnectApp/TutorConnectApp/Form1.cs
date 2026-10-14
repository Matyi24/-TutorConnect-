namespace TutorConnectApp
{
    public partial class TutorMain : Form
    {
        private readonly Dictionary<string, Func<PageBase>> pages = new();
        private PageBase? currentPage;
        private string currentKey = "";

        /// <summary>Igaz, ha a felhasználó kijelentkezett (és nem simán bezárta az appot).</summary>
        public bool LoggedOut { get; private set; }

        public TutorMain()
        {
            InitializeComponent();

            var user = ApiClient.CurrentUser ?? throw new InvalidOperationException("Nincs bejelentkezett felhasználó.");

            MinimumSize = new Size(480, 360);
            Text = $"TutorConnect – {user.Name}";

            BuildMenu(user);

            navBar.SetUser(user.Name, user.Role);
            navBar.ItemClicked += NavBar_ItemClicked;
            ClientSizeChanged += (_, _) => navBar.ApplyResponsive(ClientSize.Width);
            navBar.ApplyResponsive(ClientSize.Width);

            Theme.Changed += OnThemeChanged;
            ApplyTheme();

            navBar.Select("home");
        }

        /// <summary>
        /// A menü szerepkör szerint épül fel, a weboldalhoz hasonlóan:
        /// mindenki: Főoldal, Chat, Tantárgyak, Profil, Beállítások; diák: + Oktatók, Foglalás; oktató: + Foglalás.
        /// Új menüpont: egy sor itt + egy oldal (PageBase utód).
        /// Ikonok: Segoe MDL2 Assets karakterek.
        /// </summary>
        private void BuildMenu(UserInfo user)
        {
            AddPage("home", "Főoldal", "", () => new HomePage(user));
            AddPage("chat", "Chat", "", () => new ChatPage());
            AddPage("subjects", "Tantárgyak", "", () => new SubjectsPage(user));

            if (user.Role == "STUDENT")
                AddPage("tutors", "Oktatók", "", () => new TutorsPage());

            if (user.Role == "STUDENT" || user.Role == "TUTOR")
                AddPage("bookings", "Foglalás", "", () => new BookingsPage(user));

            AddPage("profile", "Profil", "", () => new ProfilePage());
            AddPage("settings", "Beállítások", "", () => new SettingsPage(user));

            // művelet: nem kijelölhető gomb (a kijelentkezés a Profil oldalon van)
            navBar.AddAction("theme", "Sötét mód", "");
        }

        private void AddPage(string key, string text, string icon, Func<PageBase> factory)
        {
            pages[key] = factory;
            navBar.AddItem(key, text, icon);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyBlack(this); // fekete címsor és szegély
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Theme.Changed -= OnThemeChanged;
            base.OnFormClosed(e);
        }

        private void NavBar_ItemClicked(object? sender, string key)
        {
            if (key == "theme")
            {
                Theme.SetDark(!Theme.Dark);
                return;
            }

            if (pages.TryGetValue(key, out var factory))
            {
                currentKey = key;
                ShowPage(factory());
            }
        }

        private async Task LogoutAsync()
        {
            await ApiClient.LogoutAsync();
            LoggedOut = true;
            Close();
        }

        private void OnThemeChanged()
        {
            ApplyTheme();

            // az oldal újraépül az új színekkel
            if (pages.TryGetValue(currentKey, out var factory))
                ShowPage(factory());
        }

        /// <summary>A főablak keretének színei a téma szerint (az oldalak maguktól újraépülnek).</summary>
        private void ApplyTheme()
        {
            BackColor = Theme.PageBg;
            contentPanel.BackColor = Theme.PageBg;
            navBar.ApplyTheme();

            // a téma-váltó gomb a következő állapotot mutatja
            if (Theme.Dark)
                navBar.SetItemContent("theme", "Világos mód", "");
            else
                navBar.SetItemContent("theme", "Sötét mód", "");
        }

        private void ShowPage(PageBase page)
        {
            page.NavigateRequested += key => navBar.Select(key);
            page.LogoutRequested += () => _ = LogoutAsync();

            contentPanel.SuspendLayout();

            var old = currentPage;
            currentPage = page;
            contentPanel.Controls.Add(page);
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

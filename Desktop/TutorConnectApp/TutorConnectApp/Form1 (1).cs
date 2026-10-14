namespace TutorConnectApp
{
    public partial class TutorMain : Form
    {
        /// <summary>Igaz, ha a felhasználó kijelentkezett (és nem simán bezárta az appot).</summary>
        public bool LoggedOut { get; private set; }

        public TutorMain()
        {
            InitializeComponent();

            MinimumSize = new Size(480, 360);
            if (ApiClient.CurrentUser is { } user)
                Text = $"TutorConnect – {user.Name}";

            // Ikonok: Segoe MDL2 Assets karakterek (E77B = profil, E713 = beállítások, E7E8 = kilépés)
            navBar.AddItem("profile", "Profil", "");
            navBar.AddItem("settings", "Beállítások", "");
            navBar.AddItem("logout", "Kijelentkezés", "");
            navBar.ItemClicked += NavBar_ItemClicked;
            navBar.Select("profile");

            ClientSizeChanged += (_, _) => navBar.ApplyResponsive(ClientSize.Width);
            navBar.ApplyResponsive(ClientSize.Width);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyBlack(this); // fekete címsor és szegély
        }

        private async void NavBar_ItemClicked(object? sender, string key)
        {
            if (key == "logout")
            {
                await ApiClient.LogoutAsync();
                LoggedOut = true;
                Close();
                return;
            }

            // Egyelőre csak címke; később ide jönnek az oldalak (UserControl-ok).
            pageTitleLabel.Text = key switch
            {
                "profile" => "Profil",
                "settings" => "Beállítások",
                _ => string.Empty
            };
        }
    }
}

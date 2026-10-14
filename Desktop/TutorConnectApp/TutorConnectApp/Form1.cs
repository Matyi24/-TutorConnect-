namespace TutorConnectApp
{
    public partial class TutorMain : Form
    {
        public TutorMain()
        {
            InitializeComponent();

            // Ikonok: Segoe MDL2 Assets karakterek (E77B = profil, E713 = beállítások)
            navBar.AddItem("profile", "Profil", "");
            navBar.AddItem("settings", "Beállítások", "");
            navBar.ItemClicked += NavBar_ItemClicked;
            navBar.Select("profile");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyBlack(this); // fekete címsor és szegély
        }

        private void NavBar_ItemClicked(object? sender, string key)
        {
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

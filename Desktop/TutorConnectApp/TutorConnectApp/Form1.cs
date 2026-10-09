namespace TutorConnectApp
{
    public partial class TutorMain : Form
    {
        public TutorMain()
        {
            InitializeComponent();

            navBar.AddItem("profile", "Profil");
            navBar.AddItem("settings", "Beállítások");
            navBar.ItemClicked += NavBar_ItemClicked;
            navBar.Select("profile");
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

namespace TutorConnectApp
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            AppSettings.Load(); // mentett téma és animáció-beállítás

            // Belépés nélkül nem nyílik meg a főablak. Kijelentkezés után újra a belépő jön.
            while (true)
            {
                using (var login = new LoginForm())
                {
                    if (login.ShowDialog() != DialogResult.OK)
                        return;
                }

                var main = new TutorMain();
                Application.Run(main);

                if (!main.LoggedOut)
                    return;
            }
        }
    }
}

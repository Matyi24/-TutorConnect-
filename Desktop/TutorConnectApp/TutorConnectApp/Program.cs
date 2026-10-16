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

            // Váratlan hiba esetén nem tűnik el némán az app: kiírja a hibát, és el is menti egy naplófájlba.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ReportCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportCrash(e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                ReportCrash(e.Exception);
                e.SetObserved();
            };

            AppSettings.Load(); // mentett téma és animáció-beállítás
            Ui.InitDpi();       // a képernyő nagyítása szerinti méretezéshez

            // Belépés nélkül nem nyílik meg a főablak. Kijelentkezés után újra a belépő jön.
            // Induláskor (ha az "Emlékezz rám" be volt jelölve) a mentett munkamenettel próbálkozunk.
            bool firstRun = true;
            while (true)
            {
                bool restored = false;
                if (firstRun)
                {
                    firstRun = false;
                    restored = Task.Run(() => ApiClient.TryRestoreSessionAsync()).GetAwaiter().GetResult();
                }

                if (!restored)
                {
                    using var login = new LoginForm();
                    if (login.ShowDialog() != DialogResult.OK)
                        return;
                }

                var main = new TutorMain();
                Application.Run(main);

                if (!main.LoggedOut)
                    return;
            }
        }

        /// <summary>Megjeleníti a hibát, és hozzáfűzi a %AppData%\TutorConnect\error.log fájlhoz.</summary>
        private static void ReportCrash(Exception? exception)
        {
            string details = exception?.ToString() ?? "Ismeretlen hiba";
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TutorConnect", "error.log");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}{details}{Environment.NewLine}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // a naplózás nem kritikus
            }

            string shown = details.Length > 1500 ? details[..1500] + "..." : details;
            MessageBox.Show($"Váratlan hiba történt:\n\n{shown}\n\nA részletek mentve: {logPath}",
                "TutorConnect", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

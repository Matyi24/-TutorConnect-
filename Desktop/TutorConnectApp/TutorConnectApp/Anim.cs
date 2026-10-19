using System.Text.Json;

namespace TutorConnectApp
{
    /// <summary>
    /// Egyszerű animációs motor: egyetlen közös időzítő futtatja az összes animációt (tween).
    /// Az Enabled = false (Beállítások → Animációk) esetén minden animáció azonnal kész.
    /// Csak a felületi szálról használható.
    /// </summary>
    internal static class Anim
    {
        public sealed class Tween
        {
            internal Tween(int duration, Action<double> update, Action? done, bool easeOut)
            {
                Duration = Math.Max(duration, 1);
                Update = update;
                Done = done;
                EaseOut = easeOut;
                Start = Environment.TickCount64;
            }

            internal long Start { get; }
            internal int Duration { get; }
            internal Action<double> Update { get; }
            internal Action? Done { get; }
            internal bool EaseOut { get; }
            internal bool Cancelled { get; set; }

            public void Cancel() => Cancelled = true;
        }

        public static bool Enabled { get; private set; } = true;

        private static readonly List<Tween> Active = new();
        private static readonly List<Tween> Snapshot = new(); // újrahasznosított másolat a bejáráshoz
        private static System.Windows.Forms.Timer? timer;

        public static void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            AppSettings.Save();
        }

        internal static void Init(bool enabled) => Enabled = enabled;

        /// <summary>Animáció: az update 0..1 között kapja az (ease-out) haladást.</summary>
        public static Tween Run(int durationMs, Action<double> update, Action? done = null, bool easeOut = true)
        {
            var tween = new Tween(Enabled ? durationMs : 1, update, done, easeOut);
            Active.Add(tween);

            timer ??= CreateTimer();
            if (!timer.Enabled) timer.Start();
            return tween;
        }

        /// <summary>Késleltetett művelet (animációk kikapcsolva: azonnal).</summary>
        public static Tween Delay(int ms, Action action) => Run(Enabled ? ms : 0, _ => { }, action, easeOut: false);

        public static Color Lerp(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        private static System.Windows.Forms.Timer CreateTimer()
        {
            var t = new System.Windows.Forms.Timer { Interval = 15 };
            t.Tick += (_, _) => Tick();
            return t;
        }

        private static void Tick()
        {
            long now = Environment.TickCount64;

            Snapshot.Clear();
            Snapshot.AddRange(Active);

            foreach (var tween in Snapshot)
            {
                if (tween.Cancelled)
                {
                    Active.Remove(tween);
                    continue;
                }

                double t = Math.Clamp((now - tween.Start) / (double)tween.Duration, 0, 1);
                double eased = tween.EaseOut ? 1 - Math.Pow(1 - t, 3) : t;

                try
                {
                    tween.Update(eased);
                }
                catch (ObjectDisposedException)
                {
                    tween.Cancelled = true; // a vezérlő közben megszűnt
                }
                catch (Exception)
                {
                    // egy hibás animáció ne ismétlődjön 15 ms-onként (hibaüzenet-áradat): egyszer jelez, aztán leáll
                    tween.Cancelled = true;
                    Active.Remove(tween);
                    throw;
                }

                if (t >= 1 || tween.Cancelled)
                {
                    Active.Remove(tween);
                    if (!tween.Cancelled)
                    {
                        try { tween.Done?.Invoke(); }
                        catch (ObjectDisposedException) { }
                    }
                }
            }

            if (Active.Count == 0) timer?.Stop();
        }
    }

    /// <summary>A téma és az animációk mentése (%AppData%\TutorConnect\settings.json).</summary>
    internal static class AppSettings
    {
        private sealed class Data
        {
            public bool Dark { get; set; }
            public bool Animations { get; set; } = true;
            public bool Notifications { get; set; } = true;
            public bool FavoriteAlerts { get; set; } = true;
            public bool Reminders { get; set; } = true;
            public bool HighContrast { get; set; }
            public bool MinimizeToTray { get; set; }
            public int WindowX { get; set; }
            public int WindowY { get; set; }
            public int WindowWidth { get; set; }
            public int WindowHeight { get; set; }
            public bool WindowMaximized { get; set; }
        }

        /// <summary>Emlékeztető a visszaigazolt órákra (1 órával és 10 perccel előtte).</summary>
        public static bool RemindersEnabled { get; private set; } = true;

        public static void SetReminders(bool enabled)
        {
            RemindersEnabled = enabled;
            Save();
        }

        /// <summary>Az ablak bezárása tálcára kicsinyít (a kilépés a tálcaikon menüjéből megy).</summary>
        public static bool MinimizeToTray { get; private set; }

        public static void SetMinimizeToTray(bool enabled)
        {
            MinimizeToTray = enabled;
            Save();
        }

        private static Rectangle windowBounds;
        private static bool windowMaximized;

        /// <summary>A legutóbb mentett ablakpozíció és -méret; null, ha még nincs.</summary>
        public static (Rectangle Bounds, bool Maximized)? LoadWindow() =>
            windowBounds.Width > 0 && windowBounds.Height > 0 ? (windowBounds, windowMaximized) : null;

        public static void SaveWindow(Rectangle bounds, bool maximized)
        {
            windowBounds = bounds;
            windowMaximized = maximized;
            Save();
        }

        /// <summary>Értesítés, ha egy kedvenc oktató új szabad időpontot hirdet (amíg az app fut).</summary>
        public static bool FavoriteAlertsEnabled { get; private set; } = true;

        public static void SetFavoriteAlerts(bool enabled)
        {
            FavoriteAlertsEnabled = enabled;
            Save();
        }

        /// <summary>Tálcaértesítések (új üzenet, foglalás-változás), ha az ablak nincs előtérben.</summary>
        public static bool NotificationsEnabled { get; private set; } = true;

        public static void SetNotifications(bool enabled)
        {
            NotificationsEnabled = enabled;
            Save();
        }

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TutorConnect", "settings.json");

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    // első indítás: ha a Windows magas kontrasztú módban van, az app is úgy indul
                    Theme.Init(false, SystemInformation.HighContrast);
                    return;
                }

                var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath));
                if (data == null) return;
                Theme.Init(data.Dark, data.HighContrast);
                MinimizeToTray = data.MinimizeToTray;
                RemindersEnabled = data.Reminders;
                windowBounds = new Rectangle(data.WindowX, data.WindowY, data.WindowWidth, data.WindowHeight);
                windowMaximized = data.WindowMaximized;
                Anim.Init(data.Animations);
                NotificationsEnabled = data.Notifications;
                FavoriteAlertsEnabled = data.FavoriteAlerts;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // sérült vagy nem olvasható fájl: az alapértelmezett beállítások maradnak
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(new Data
                {
                    Dark = Theme.DarkSetting,
                    Animations = Anim.Enabled,
                    Notifications = NotificationsEnabled,
                    FavoriteAlerts = FavoriteAlertsEnabled,
                    HighContrast = Theme.HighContrast,
                    Reminders = RemindersEnabled,
                    MinimizeToTray = MinimizeToTray,
                    WindowX = windowBounds.X,
                    WindowY = windowBounds.Y,
                    WindowWidth = windowBounds.Width,
                    WindowHeight = windowBounds.Height,
                    WindowMaximized = windowMaximized
                }));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // a mentés nem kritikus
            }
        }
    }
}

namespace TutorConnectApp
{
    /// <summary>
    /// Színek és téma. A márkaszínek a weboldalról (vsc/css/style.css) valók és mindkét módban ugyanazok,
    /// a felületek színei (háttér, kártya, szöveg...) a világos / sötét mód szerint változnak.
    /// Váltás: Theme.SetDark(true) → a Changed esemény újraépíti a felületet.
    /// </summary>
    internal static class Theme
    {
        // márkaszínek
        public static readonly Color MainBlue = ColorTranslator.FromHtml("#4C6EF5");
        public static readonly Color MainBlueDark = ColorTranslator.FromHtml("#3B5BDB");
        public static readonly Color White = Color.White;

        // állapotszínek
        public static readonly Color Success = ColorTranslator.FromHtml("#2F9E44");
        public static readonly Color Warning = ColorTranslator.FromHtml("#F08C00");
        public static readonly Color Danger = ColorTranslator.FromHtml("#E03131");

        public const string FontFamily = "Segoe UI";

        public static bool Dark { get; private set; }

        /// <summary>A téma váltásakor fut le (a főablak újraépíti az oldalt).</summary>
        public static event Action? Changed;

        public static void SetDark(bool dark)
        {
            if (Dark == dark) return;
            Dark = dark;
            AppSettings.Save();
            Changed?.Invoke();
        }

        /// <summary>Induláskor a mentett beállításból, esemény nélkül.</summary>
        public static void Init(bool dark) => Dark = dark;

        // felületek (módfüggő)
        public static Color PageBg => Pick("#EAEFFF", "#0F1222");
        public static Color CardBg => Pick("#FFFFFF", "#181C33");
        public static Color CardBorder => Pick("#D5DDF8", "#2A3052");
        public static Color Text => Pick("#212529", "#E8EAF6");
        public static Color Muted => Pick("#6B7280", "#9AA3C7");
        public static Color ChipBg => Pick("#DBE4FF", "#26305E");
        public static Color ChipFg => Pick("#3B5BDB", "#BAC8FF");
        public static Color InputBg => Pick("#FFFFFF", "#20254A");
        public static Color SoftHover => Pick("#F1F3F5", "#222850");

        // oldalsáv
        public static Color NavBg => Pick("#3B5BDB", "#0B0E1C");
        public static Color NavActive => Pick("#5C7CFA", "#2D3A8C");

        private static Color Pick(string light, string dark) => ColorTranslator.FromHtml(Dark ? dark : light);
    }
}

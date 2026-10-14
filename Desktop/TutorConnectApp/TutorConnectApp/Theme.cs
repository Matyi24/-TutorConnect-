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

        // egyedi, több helyen használt színek (egyszer feldolgozva)
        public static readonly Color Star = ColorTranslator.FromHtml("#F59F00");
        public static readonly Color SwitchOffLight = ColorTranslator.FromHtml("#B8C0D8");
        public static readonly Color SwitchOffDark = ColorTranslator.FromHtml("#3A4170");
        public static readonly Color HeroLight1 = ColorTranslator.FromHtml("#5C7CFA");
        public static readonly Color HeroDark1 = ColorTranslator.FromHtml("#3B5BDB");
        public static readonly Color HeroDark2 = ColorTranslator.FromHtml("#2B3A8C");
        public static readonly Color LoginDark1 = ColorTranslator.FromHtml("#0B0E1C");
        public static readonly Color LoginDark2 = ColorTranslator.FromHtml("#232C5E");

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

        // felületek (módfüggő): a színek egyszer kerülnek feldolgozásra, nem minden rajzoláskor
        private readonly record struct Pair(Color Light, Color Dark)
        {
            public Color Get(bool dark) => dark ? Dark : Light;
        }

        private static Pair P(string light, string dark) => new(ColorTranslator.FromHtml(light), ColorTranslator.FromHtml(dark));

        private static readonly Pair PageBgPair = P("#EAEFFF", "#0F1222");
        private static readonly Pair CardBgPair = P("#FFFFFF", "#181C33");
        private static readonly Pair CardBorderPair = P("#D5DDF8", "#2A3052");
        private static readonly Pair TextPair = P("#212529", "#E8EAF6");
        private static readonly Pair MutedPair = P("#6B7280", "#9AA3C7");
        private static readonly Pair ChipBgPair = P("#DBE4FF", "#26305E");
        private static readonly Pair ChipFgPair = P("#3B5BDB", "#BAC8FF");
        private static readonly Pair InputBgPair = P("#FFFFFF", "#20254A");
        private static readonly Pair SoftHoverPair = P("#F1F3F5", "#222850");
        private static readonly Pair NavBgPair = P("#3B5BDB", "#0B0E1C");
        private static readonly Pair NavActivePair = P("#5C7CFA", "#2D3A8C");

        public static Color PageBg => PageBgPair.Get(Dark);
        public static Color CardBg => CardBgPair.Get(Dark);
        public static Color CardBorder => CardBorderPair.Get(Dark);
        public static Color Text => TextPair.Get(Dark);
        public static Color Muted => MutedPair.Get(Dark);
        public static Color ChipBg => ChipBgPair.Get(Dark);
        public static Color ChipFg => ChipFgPair.Get(Dark);
        public static Color InputBg => InputBgPair.Get(Dark);
        public static Color SoftHover => SoftHoverPair.Get(Dark);

        // oldalsáv
        public static Color NavBg => NavBgPair.Get(Dark);
        public static Color NavActive => NavActivePair.Get(Dark);
    }
}

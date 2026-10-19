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

        private static bool darkSetting;

        /// <summary>Sötét felületek: a sötét mód vagy a magas kontraszt esetén igaz.</summary>
        public static bool Dark => darkSetting || HighContrast;

        /// <summary>Magas kontrasztú megjelenés (fekete háttér, fehér szöveg és keretek) a jobb olvashatóságért.</summary>
        public static bool HighContrast { get; private set; }

        /// <summary>A téma váltásakor fut le (a főablak újraépíti az oldalt).</summary>
        public static event Action? Changed;

        public static void SetDark(bool dark)
        {
            if (darkSetting == dark) return;
            darkSetting = dark;
            AppSettings.Save();
            Changed?.Invoke();
        }

        public static void SetHighContrast(bool enabled)
        {
            if (HighContrast == enabled) return;
            HighContrast = enabled;
            AppSettings.Save();
            Changed?.Invoke();
        }

        /// <summary>Induláskor a mentett beállításból, esemény nélkül.</summary>
        public static void Init(bool dark, bool highContrast = false)
        {
            darkSetting = dark;
            HighContrast = highContrast;
        }

        /// <summary>A mentett sötét-mód beállítás (a magas kontraszttól függetlenül).</summary>
        public static bool DarkSetting => darkSetting;

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

        // magas kontraszt: fekete alap, fehér szöveg és keretek, sárga kiemelés
        private static readonly Color HcBlack = Color.Black;
        private static readonly Color HcWhite = Color.White;
        private static readonly Color HcMuted = ColorTranslator.FromHtml("#E6E6E6");
        private static readonly Color HcChipBg = ColorTranslator.FromHtml("#262626");
        private static readonly Color HcChipFg = ColorTranslator.FromHtml("#FFFF66");
        private static readonly Color HcHover = ColorTranslator.FromHtml("#333333");
        private static readonly Color HcNavActive = ColorTranslator.FromHtml("#0033CC");

        public static Color PageBg => HighContrast ? HcBlack : PageBgPair.Get(Dark);
        public static Color CardBg => HighContrast ? HcBlack : CardBgPair.Get(Dark);
        public static Color CardBorder => HighContrast ? HcWhite : CardBorderPair.Get(Dark);
        public static Color Text => HighContrast ? HcWhite : TextPair.Get(Dark);
        public static Color Muted => HighContrast ? HcMuted : MutedPair.Get(Dark);
        public static Color ChipBg => HighContrast ? HcChipBg : ChipBgPair.Get(Dark);
        public static Color ChipFg => HighContrast ? HcChipFg : ChipFgPair.Get(Dark);
        public static Color InputBg => HighContrast ? HcBlack : InputBgPair.Get(Dark);
        public static Color SoftHover => HighContrast ? HcHover : SoftHoverPair.Get(Dark);

        // oldalsáv
        public static Color NavBg => HighContrast ? HcBlack : NavBgPair.Get(Dark);
        public static Color NavActive => HighContrast ? HcNavActive : NavActivePair.Get(Dark);
    }
}

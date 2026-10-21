using System.Drawing.Drawing2D;
using System.Globalization;

namespace TutorConnectApp
{
    /// <summary>Közös kinézet-segédek: feliratok, gombok, kártyák, rajzolás.</summary>
    internal static class Ui
    {
        public static readonly CultureInfo Hu = CultureInfo.GetCultureInfo("hu-HU");

        // ------------------------------------------------------------
        // DPI-skálázás: minden méretet 96 DPI-s ("tervezési") képpontban adunk meg, és a képernyő
        // nagyítása (pl. 125%, 150%) szerint váltjuk át. A betűk pontban vannak megadva, ezek maguktól nőnek,
        // ezért a méreteknek is együtt kell nőniük, különben a szöveg levágódik vagy átfedi egymást.
        // ------------------------------------------------------------

        public static float Scale { get; private set; } = 1F;

        /// <summary>A skála beállítása a képernyő DPI-értékéből (96 = 100%).</summary>
        public static void SetDpi(int dpi) => Scale = Math.Max(dpi, 96) / 96F;

        /// <summary>Induláskor: a rendszer DPI-je (a főablak később a saját monitorjára pontosítja).</summary>
        public static void InitDpi()
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            SetDpi((int)g.DpiX);
        }

        public static int S(int px) => (int)Math.Round(px * Scale);

        public static float S(float px) => px * Scale;

        public static Padding Pad(int all) => new(S(all));

        public static Padding Pad(int left, int top, int right, int bottom) => new(S(left), S(top), S(right), S(bottom));

        public static Size Sz(int width, int height) => new(S(width), S(height));

        public static Point Pt(int x, int y) => new(S(x), S(y));

        public static Rectangle Rc(int x, int y, int width, int height) => new(S(x), S(y), S(width), S(height));

        // A betűtípusok drága GDI-erőforrások: egyszer jönnek létre, és megosztva használjuk őket
        // (rajzoláskor nem szabad megsemmisíteni, ezért nincs `using` mellettük).
        private static readonly Dictionary<(string Family, float Size, FontStyle Style), Font> FontCache = new();

        private static Font Cached(string family, float size, FontStyle style)
        {
            var key = (family, size, style);
            if (!FontCache.TryGetValue(key, out var font))
                FontCache[key] = font = new Font(family, size, style);
            return font;
        }

        private static readonly Dictionary<Font, int> LineHeights = new();

        /// <summary>Egy sor magassága képpontban az adott betűtípussal (a megosztott, gyorsítótárazott betűkhöz).</summary>
        public static int LineHeight(Font font)
        {
            if (!LineHeights.TryGetValue(font, out int height))
            {
                height = TextRenderer.MeasureText("Ag", font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Height;
                LineHeights[font] = height;
            }
            return height;
        }

        public static Font MakeFont(float size, FontStyle style = FontStyle.Regular) => Cached(Theme.FontFamily, size, style);

        /// <summary>Segoe MDL2 Assets ikonbetű.</summary>
        public static Font IconFont(float size) => Cached("Segoe MDL2 Assets", size, FontStyle.Regular);

        /// <summary>Segoe UI Symbol (csillag, nyilak, matematikai jelek).</summary>
        public static Font SymbolFont(float size, FontStyle style = FontStyle.Regular) => Cached("Segoe UI Symbol", size, style);

        // ------------------------------------------------------------
        // Szövegek
        // ------------------------------------------------------------

        public static string RoleName(string role) => role switch
        {
            "STUDENT" => "Diák",
            "TUTOR" => "Oktató",
            "ADMIN" => "Adminisztrátor",
            _ => role
        };

        public static string RoleIntro(string role) => role switch
        {
            "STUDENT" => "Találj oktatót, foglalj időpontot, és kövesd az óráidat.",
            "TUTOR" => "Kezeld a foglalásaidat, és add meg, mikor érsz rá tanítani.",
            "ADMIN" => "Rendszergazdai nézet.",
            _ => ""
        };

        public static string FirstName(string fullName)
        {
            // magyar névsorrend: "Kovács Bence" → a keresztnév az utolsó szó
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? fullName : parts[^1];
        }

        public static string Initials(string name)
        {
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            string text = parts.Length == 1 ? parts[0][..1] : parts[0][..1] + parts[^1][..1];
            return text.ToUpper(Hu);
        }

        /// <summary>A tantárgy-kategóriák színe (kártyák, jelzések).</summary>
        public static Color CategoryColor(string? category) => ColorTranslator.FromHtml((category ?? "").ToLowerInvariant() switch
        {
            "reál" => "#4C6EF5",
            "humán" => "#F76707",
            "nyelvek" => "#12B886",
            "természettudomány" => "#40C057",
            "művészet" => "#BE4BDB",
            "gazdaság" => "#F59F00",
            _ => "#868E96"
        });

        public static string Money(decimal value) => value.ToString("N0", Hu) + " Ft";

        /// <summary>Fájlméret olvasható alakban (B, KB, MB).</summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
            return $"{bytes / (1024.0 * 1024):0.#} MB";
        }

        /// <summary>Kis- és nagybetűre, ékezetre érzéketlen "tartalmazza" ("o" megtalálja az "ő"-t is).</summary>
        public static bool ContainsText(string text, string query) => TextSearch.Contains(text, query);

        public static string FormatDay(DateTime value) => value.ToLocalTime().ToString("yyyy. MM. dd., dddd", Hu);

        public static string FormatTimeRange(DateTime start, DateTime end) =>
            $"{start.ToLocalTime().ToString("HH:mm", Hu)}–{end.ToLocalTime().ToString("HH:mm", Hu)}";

        public static string FormatRange(DateTime start, DateTime end) =>
            $"{start.ToLocalTime().ToString("yyyy. MM. dd., dddd HH:mm", Hu)} – {end.ToLocalTime().ToString("HH:mm", Hu)}";

        // ------------------------------------------------------------
        // Vezérlő-gyárak
        // ------------------------------------------------------------

        public static Label Lbl(string text, float size = 10F, Color? color = null, bool bold = false, bool wrap = false, Padding? margin = null) => new()
        {
            Text = text,
            AutoSize = true,
            Font = MakeFont(size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color ?? Theme.Text,
            BackColor = Color.Transparent,
            Tag = wrap ? "wrap" : null, // a CardPanel a "wrap" jelölésű elemeket a kártya szélességére tördeli
            Margin = margin ?? Ui.Pad(0, 0, 0, 4)
        };

        public static Label Section(string text) => Lbl(text, 13F, Theme.Text, bold: true, margin: Ui.Pad(0, 10, 0, 12));

        /// <summary>Semleges (téma szerinti) címke.</summary>
        public static Label Chip(string text) => Chip(text, Theme.ChipBg, Theme.ChipFg);

        public static Label Chip(string text, Color back, Color? fore = null) => new()
        {
            Text = text,
            AutoSize = true,
            BackColor = back,
            ForeColor = fore ?? Theme.White,
            Font = MakeFont(8.5F, FontStyle.Bold),
            Padding = Ui.Pad(9, 3, 9, 3),
            Margin = Ui.Pad(0, 0, 8, 6)
        };

        /// <summary>Kitöltött (outline = false) vagy körvonalas gomb finom hover-animációval.</summary>
        public static HoverButton Btn(string text, Color back, Color fore, EventHandler? onClick, bool outline = false)
        {
            var button = new HoverButton { Text = text, ForeColor = fore };
            if (outline)
            {
                button.SetColors(Theme.CardBg, Theme.SoftHover, Theme.CardBorder);
            }
            else
            {
                button.SetColors(back, Anim.Lerp(back, Color.Black, 0.14), null);
            }
            if (onClick != null) button.Click += onClick;
            return button;
        }

        public static HoverButton PrimaryBtn(string text, EventHandler? onClick) => Btn(text, Theme.MainBlue, Theme.White, onClick);

        /// <summary>Vízszintes sor (átlátszó), pl. kártyán belül elemek egymás mellett.</summary>
        public static FlowLayoutPanel Row() => new()
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Ui.Pad(0, 0, 0, 4),
            Padding = Ui.Pad(0)
        };

        /// <summary>Tördelődő sor (csempék, címkék). A CardPanel a szélességét a kártyához igazítja.</summary>
        public static FlowLayoutPanel WrapRow() => new()
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Ui.Pad(0),
            Padding = Ui.Pad(0),
            Tag = "wrap"
        };

        /// <summary>A konténer összes gyerekének eltávolítása és megsemmisítése.</summary>
        public static void Reset(Control host)
        {
            // egyben ürítjük ki (nem darabonként), így az elrendezés csak egyszer számolódik újra
            var children = host.Controls.Cast<Control>().ToArray();
            if (children.Length == 0) return;

            host.SuspendLayout();
            host.Controls.Clear();
            foreach (var child in children)
                child.Dispose();
            host.ResumeLayout(true);
        }

        public static Control Loading(string text = "Betöltés...")
        {
            var row = Row();
            row.Margin = Ui.Pad(0, 8, 0, 8);
            row.Controls.Add(new Spinner());
            row.Controls.Add(Lbl(text, 10.5F, Theme.Muted, margin: Ui.Pad(10, 5, 0, 0)));
            return row;
        }

        public static CardPanel MessageCard(string text, string glyph = "\uE946")
        {
            var card = new CardPanel();
            var row = Row();
            var icon = new Label
            {
                Text = glyph,
                Font = IconFont(18F),
                ForeColor = Theme.MainBlue,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = Ui.Pad(0, 0, 14, 0)
            };
            row.Controls.Add(icon);
            row.Controls.Add(Lbl(text, 10.5F, Theme.Muted, margin: Ui.Pad(0, 4, 0, 0)));
            card.Controls.Add(row);
            return card;
        }

        public static Control KeyValue(string key, string value)
        {
            var row = Row();
            var k = Lbl(key, 10F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 0));
            k.AutoSize = false;
            k.Size = Ui.Sz(150, 24);
            row.Controls.Add(k);
            row.Controls.Add(Lbl(value, 10.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 0)));
            return row;
        }

        // ------------------------------------------------------------
        // Rajzolás
        // ------------------------------------------------------------

        /// <summary>A vezérlő mögötti tényleges háttérszín (az átlátszó szülőket átugorva).</summary>
        public static Color EffectiveBack(Control? control)
        {
            while (control != null)
            {
                if (control.BackColor.A == 255) return control.BackColor;
                control = control.Parent;
            }
            return Theme.PageBg;
        }

        public static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            int d = Math.Max(Math.Min(radius * 2, Math.Min(r.Width, r.Height)), 2);
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Pontozott fókuszkeret a billentyűzettel kijelölt, egyedi rajzolású vezérlőkhöz.</summary>
        public static void DrawFocusRing(Graphics g, Rectangle bounds, int radius)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.HighContrast ? Color.Yellow : Theme.Text, S(2F)) { DashStyle = DashStyle.Dot };
            using var path = RoundedPath(Rectangle.Inflate(bounds, -S(2), -S(2)), Math.Max(radius - S(2), 2));
            g.DrawPath(pen, path);
            g.SmoothingMode = old;
        }

        // közös, középre igazító szövegformátum (megosztva, rajzoláskor nem kell újat létrehozni)
        private static readonly StringFormat CenterFormat = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        public static void DrawAvatar(Graphics g, Rectangle bounds, string initials, Color fill, Color text)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(fill))
                g.FillEllipse(brush, bounds);

            var font = MakeFont(MathF.Round(Math.Max(bounds.Width / Scale * 0.3F, 8F) * 2) / 2, FontStyle.Bold); // fél pontra kerekítve, hogy kevés változat jöjjön létre
            using var textBrush = new SolidBrush(text);
            g.DrawString(initials, font, textBrush, bounds, CenterFormat);
            g.SmoothingMode = old;
        }
    }
}

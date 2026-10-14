using System.Drawing.Drawing2D;
using System.Globalization;

namespace TutorConnectApp
{
    /// <summary>Közös kinézet-segédek: színek, feliratok, gombok, kártyák.</summary>
    internal static class Ui
    {
        public static readonly CultureInfo Hu = CultureInfo.GetCultureInfo("hu-HU");

        public static readonly Color Muted = ColorTranslator.FromHtml("#6B7280");
        public static readonly Color Success = ColorTranslator.FromHtml("#2F9E44");
        public static readonly Color Warning = ColorTranslator.FromHtml("#F08C00");
        public static readonly Color Danger = ColorTranslator.FromHtml("#E03131");
        public static readonly Color Border = ColorTranslator.FromHtml("#C5D0F5");

        public static Font MakeFont(float size, FontStyle style = FontStyle.Regular) => new(Theme.FontFamily, size, style);

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
            "TUTOR" => "Kezeld a foglalásaidat, és tartsd a kapcsolatot a diákjaiddal.",
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

        public static string Money(decimal value) => value.ToString("N0", Hu) + " Ft";

        public static string FormatRange(DateTime start, DateTime end)
        {
            var s = start.ToLocalTime();
            var e = end.ToLocalTime();
            return $"{s.ToString("yyyy. MM. dd., dddd HH:mm", Hu)} – {e.ToString("HH:mm", Hu)}";
        }

        // ------------------------------------------------------------
        // Vezérlő-gyárak
        // ------------------------------------------------------------

        public static Label Lbl(string text, float size = 10F, Color? color = null, bool bold = false, bool wrap = false, Padding? margin = null) => new()
        {
            Text = text,
            AutoSize = true,
            Font = MakeFont(size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color ?? Theme.DarkText,
            BackColor = Color.Transparent,
            Tag = wrap ? "wrap" : null, // a CardPanel a "wrap" feliratokat a kártya szélességére tördeli
            Margin = margin ?? new Padding(0, 0, 0, 4)
        };

        public static Label Section(string text) => Lbl(text, 13F, Theme.DarkText, bold: true, margin: new Padding(0, 8, 0, 10));

        public static Label Chip(string text, Color back, Color? fore = null) => new()
        {
            Text = text,
            AutoSize = true,
            BackColor = back,
            ForeColor = fore ?? Theme.White,
            Font = MakeFont(8.5F, FontStyle.Bold),
            Padding = new Padding(8, 3, 8, 3),
            Margin = new Padding(0, 0, 8, 6)
        };

        public static Button Btn(string text, Color back, Color fore, EventHandler? onClick, bool outline = false)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore,
                Font = MakeFont(9.5F, FontStyle.Bold),
                Padding = new Padding(12, 4, 12, 4),
                Margin = new Padding(0, 6, 8, 0),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = outline ? 1 : 0;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = outline ? Theme.LightGrey : ControlPaint.Dark(back, 0.08F);
            if (onClick != null) button.Click += onClick;
            return button;
        }

        /// <summary>Vízszintes sor (átlátszó), pl. kártyán belül elemek egymás mellett.</summary>
        public static FlowLayoutPanel Row() => new()
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4),
            Padding = new Padding(0)
        };

        /// <summary>Tördelődő sor (csempék, címkék). A CardPanel a szélességét a kártyához igazítja.</summary>
        public static FlowLayoutPanel WrapRow() => new()
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Tag = "wrap"
        };

        public static CardPanel StatTile(string value, string caption, Color accent)
        {
            var tile = new CardPanel
            {
                Margin = new Padding(0, 0, 16, 16),
                MinimumSize = new Size(190, 0)
            };
            tile.Controls.Add(Lbl(value, 24F, accent, bold: true, margin: new Padding(0, 0, 0, 0)));
            tile.Controls.Add(Lbl(caption, 9.5F, Muted, margin: new Padding(0)));
            return tile;
        }

        public static CardPanel MessageCard(string text)
        {
            var card = new CardPanel();
            card.Controls.Add(Lbl(text, 10.5F, Muted, wrap: true, margin: new Padding(0)));
            return card;
        }

        public static Control KeyValue(string key, string value)
        {
            var row = Row();
            var k = Lbl(key, 10F, Muted, margin: new Padding(0, 0, 0, 0));
            k.AutoSize = false;
            k.Size = new Size(150, 24);
            row.Controls.Add(k);
            row.Controls.Add(Lbl(value, 10.5F, Theme.DarkText, bold: true, margin: new Padding(0, 0, 0, 0)));
            return row;
        }

        // ------------------------------------------------------------
        // Rajzolás
        // ------------------------------------------------------------

        public static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawAvatar(Graphics g, Rectangle bounds, string initials, Color fill, Color text)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(fill))
                g.FillEllipse(brush, bounds);

            using var font = MakeFont(Math.Max(bounds.Width * 0.3F, 8F), FontStyle.Bold);
            using var textBrush = new SolidBrush(text);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(initials, font, textBrush, bounds, format);
            g.SmoothingMode = old;
        }
    }

    /// <summary>Fehér, lekerekített kártya, a tartalma függőlegesen egymás alá kerül, magassága automatikus.</summary>
    internal class CardPanel : FlowLayoutPanel
    {
        public CardPanel()
        {
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Theme.White;
            Padding = new Padding(20, 16, 20, 16);
            Margin = new Padding(0, 0, 0, 16);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            // a "wrap" jelölésű elemek a kártya belső szélességéhez igazodnak (hosszú szöveg tördelése)
            int inner = Math.Max(ClientSize.Width - Padding.Horizontal, 0);
            foreach (Control child in Controls)
            {
                if (child.Tag as string == "wrap" && inner > 0)
                    child.MaximumSize = new Size(inner, 0);
            }

            if (Width > 0 && Height > 0)
            {
                using var path = Ui.RoundedPath(new Rectangle(0, 0, Width, Height), 12);
                var old = Region;
                Region = new Region(path);
                old?.Dispose();
            }
        }
    }

    /// <summary>Kör alakú avatar a név kezdőbetűivel.</summary>
    internal class AvatarCircle : Control
    {
        private readonly string initials;

        public AvatarCircle(string name, int size)
        {
            initials = Ui.Initials(name);
            Size = new Size(size, size);
            Margin = new Padding(0, 0, 14, 0);
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Ui.DrawAvatar(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), initials, Theme.MainBlue, Theme.White);
        }
    }
}

using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.RegularExpressions;

namespace TutorConnectApp
{
    /// <summary>
    /// A dialógusok színátmenetes fejléce: ikon körben, cím és (tördelt) alcím. A magassága a szövegből jön,
    /// így semmilyen nagyításnál nem fedi egymást a tartalom.
    /// </summary>
    internal sealed class DialogHeader : Control
    {
        private readonly string title;
        private readonly string subtitle;
        private readonly string glyph;
        private readonly Font titleFont = Ui.MakeFont(18F, FontStyle.Bold);
        private readonly Font subtitleFont = Ui.MakeFont(10.5F);
        private readonly Rectangle iconRect;
        private readonly Rectangle titleRect;
        private readonly Rectangle subtitleRect;

        public DialogHeader(string title, string subtitle, string glyph, int width)
        {
            this.title = title;
            this.subtitle = subtitle;
            this.glyph = glyph;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            int padX = Ui.S(28);
            int padY = Ui.S(22);
            int iconSize = glyph.Length > 0 ? Ui.S(56) : 0;
            int textX = padX + (iconSize > 0 ? iconSize + Ui.S(18) : 0);
            int textWidth = Math.Max(width - textX - padX, Ui.S(120));

            int titleHeight = Ui.LineHeight(titleFont);
            int subtitleHeight = subtitle.Length == 0
                ? 0
                : TextRenderer.MeasureText(subtitle, subtitleFont, new Size(textWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
            int textBlock = titleHeight + (subtitleHeight > 0 ? Ui.S(4) + subtitleHeight : 0);

            int height = Math.Max(textBlock, iconSize) + padY * 2;
            iconRect = new Rectangle(padX, (height - iconSize) / 2, iconSize, iconSize);
            titleRect = new Rectangle(textX, (height - textBlock) / 2, textWidth, titleHeight + 2);
            subtitleRect = new Rectangle(textX, titleRect.Y + titleHeight + Ui.S(4), textWidth, subtitleHeight + 2);

            Height = height;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (Width < 10 || Height < 10) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width, Height);
            Color c1 = Theme.Dark ? Theme.HeroDark1 : Theme.HeroLight1;
            Color c2 = Theme.Dark ? Theme.HeroDark2 : Theme.MainBlueDark;
            using (var brush = new LinearGradientBrush(rect, c1, c2, 20F))
                g.FillRectangle(brush, rect);

            // díszítő körök
            g.SetClip(rect);
            using (var circle = new SolidBrush(Color.FromArgb(30, Color.White)))
            {
                g.FillEllipse(circle, Width - Ui.S(170), -Ui.S(70), Ui.S(240), Ui.S(240));
                g.FillEllipse(circle, Width - Ui.S(80), Ui.S(40), Ui.S(130), Ui.S(130));
            }
            g.ResetClip();

            if (glyph.Length > 0)
            {
                using (var fill = new SolidBrush(Color.FromArgb(52, Color.White)))
                    g.FillEllipse(fill, iconRect);
                using var ring = new Pen(Color.FromArgb(110, Color.White), Ui.S(1.5F));
                g.DrawEllipse(ring, iconRect);

                bool symbol = glyph[0] < ''; // a Segoe MDL2 ikonok a privát Unicode-tartományban vannak
                var font = symbol ? Ui.MakeFont(18F, FontStyle.Bold) : Ui.IconFont(20F);
                using var iconBrush = new SolidBrush(Color.White);
                using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(glyph, font, iconBrush, iconRect, center);
            }

            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, title, titleFont, titleRect, Color.White, flags | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (subtitle.Length > 0)
                TextRenderer.DrawText(g, subtitle, subtitleFont, subtitleRect, Color.FromArgb(232, 238, 255), flags | TextFormatFlags.WordBreak);
        }
    }

    /// <summary>
    /// Szakaszcím a dialógusban. Az "1   Szöveg" alakú címeknél a szám kék körben jelenik meg (lépések),
    /// egyébként egy kis kék jelzősáv áll a szöveg előtt.
    /// </summary>
    internal sealed class SectionHeading : Control
    {
        private static readonly Regex Numbered = new(@"^(\d)\s+(.*)$");

        private readonly string number;
        private readonly string text;
        private readonly Font textFont = Ui.MakeFont(11.5F, FontStyle.Bold);

        public SectionHeading(string raw, int width)
        {
            var match = Numbered.Match(raw);
            number = match.Success ? match.Groups[1].Value : "";
            text = match.Success ? match.Groups[2].Value : raw;

            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Margin = Ui.Pad(0, 12, 0, 6);
            Size = new Size(width, Math.Max(Ui.LineHeight(textFont), Ui.S(26)) + Ui.S(14));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // a cím alatt vékony vonal választja el a szakasz tartalmától
            int titleHeight = Height - Ui.S(8);
            using (var rule = new Pen(Theme.CardBorder))
                g.DrawLine(rule, 0, Height - 1, Width, Height - 1);

            int textX;
            if (number.Length > 0)
            {
                int d = Ui.S(26);
                var circle = new Rectangle(0, (titleHeight - d) / 2, d, d);
                using (var fill = new SolidBrush(Theme.MainBlue))
                    g.FillEllipse(fill, circle);
                using var white = new SolidBrush(Color.White);
                using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(number, Ui.MakeFont(9.5F, FontStyle.Bold), white, circle, center);
                textX = d + Ui.S(12);
            }
            else
            {
                var bar = new Rectangle(0, (titleHeight - Ui.S(20)) / 2, Ui.S(4), Ui.S(20));
                using var path = Ui.RoundedPath(bar, Ui.S(2));
                using var fill = new SolidBrush(Theme.MainBlue);
                g.FillPath(fill, path);
                textX = Ui.S(14);
            }

            TextRenderer.DrawText(g, text, textFont, new Rectangle(textX, 0, Math.Max(Width - textX, 0), titleHeight), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// Egy szakasz doboza a dialógusban: lekerekített, a törzstől eltérő hátterű keret, benne a szakaszcím
    /// és a hozzá tartozó mezők. Így jól látszik, melyik cím alá mi tartozik.
    /// </summary>
    internal sealed class SectionGroup : FlowLayoutPanel
    {
        public SectionGroup(int width)
        {
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MinimumSize = new Size(width, 0);
            MaximumSize = new Size(width, 0);
            BackColor = Theme.PageBg; // átlátszatlan, így a belső elemek erre a színre rajzolják a hátterüket
            Padding = Ui.Pad(20, 2, 20, 8);
            Margin = Ui.Pad(0, 0, 0, 16);
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            if (Width < 4 || Height < 4) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(14));
            using var fill = new SolidBrush(BackColor);
            using var border = new Pen(Theme.CardBorder);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }
    }
}

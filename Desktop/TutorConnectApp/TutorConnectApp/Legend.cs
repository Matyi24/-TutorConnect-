using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>
    /// Jelmagyarázat: színes pont és mellette a jelentése, egy sorban, szükség esetén tördelve.
    /// Lekerekített "pirulában" áll, hogy elkülönüljön a környezetétől.
    /// </summary>
    internal sealed class LegendBar : Control
    {
        internal readonly record struct Entry(Color Color, string Text, bool Dashed = false, bool Hatched = false);

        private readonly List<Entry> entries;
        private readonly string title;
        private readonly Font textFont = Ui.MakeFont(9.5F);
        private readonly Font titleFont = Ui.MakeFont(9.5F, FontStyle.Bold);
        private List<(Rectangle Dot, Rectangle Text, Entry Entry)> layout = new();
        private Rectangle titleRect;

        private static int PadX => Ui.S(16);
        private static int PadY => Ui.S(10);
        private static int Gap => Ui.S(22);

        public LegendBar(string title, IEnumerable<Entry> entries)
        {
            this.title = title;
            this.entries = entries.ToList();
            Tag = "fill"; // a kártyán / oldalon a teljes szélességet kitölti
            Margin = Ui.Pad(0, 0, 0, 12);
            AccessibleRole = AccessibleRole.Grouping;
            AccessibleName = (title.Length > 0 ? title + ": " : "") + string.Join(", ", this.entries.Select(e => e.Text));
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Ui.S(44);
        }

        private void DoLayout()
        {
            layout = new List<(Rectangle, Rectangle, Entry)>();
            int width = Math.Max(Width - PadX * 2, Ui.S(100));
            int lineHeight = Math.Max(Ui.LineHeight(textFont), Ui.S(18));
            int dot = Ui.S(12);

            int x = PadX;
            int y = PadY;

            if (title.Length > 0)
            {
                int titleWidth = TextRenderer.MeasureText(title, titleFont).Width;
                titleRect = new Rectangle(x, y, titleWidth, lineHeight);
                x += titleWidth + Ui.S(14);
            }
            else
            {
                titleRect = Rectangle.Empty;
            }

            foreach (var entry in entries)
            {
                int textWidth = TextRenderer.MeasureText(entry.Text, textFont).Width;
                int itemWidth = dot + Ui.S(8) + textWidth;

                if (x + itemWidth > PadX + width && x > PadX)
                {
                    x = PadX;
                    y += lineHeight + Ui.S(6);
                }

                var dotRect = new Rectangle(x, y + (lineHeight - dot) / 2, dot, dot);
                var textRect = new Rectangle(x + dot + Ui.S(8), y, textWidth + Ui.S(2), lineHeight);
                layout.Add((dotRect, textRect, entry));
                x += itemWidth + Gap;
            }

            int wanted = y + lineHeight + PadY;
            if (Height != wanted) Height = wanted;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width > 0) DoLayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            if (Width < 20) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(12)))
            using (var fill = new SolidBrush(Theme.CardBg))
            using (var border = new Pen(Theme.CardBorder))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            if (!titleRect.IsEmpty)
                TextRenderer.DrawText(g, title, titleFont, titleRect, Theme.Text,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

            foreach (var (dot, text, entry) in layout)
            {
                if (entry.Hatched)
                {
                    // a szabadság a naptárban csíkozott: a jelmagyarázatban is
                    using var hatch = new HatchBrush(HatchStyle.WideUpwardDiagonal, entry.Color, Color.Transparent);
                    g.FillEllipse(hatch, dot);
                    using var ring = new Pen(entry.Color, Ui.S(1.2F));
                    g.DrawEllipse(ring, dot);
                }
                else if (entry.Dashed)
                {
                    // a szabad időpont a naptárban szaggatott keretű
                    using var pen = new Pen(entry.Color, Ui.S(1.6F)) { DashStyle = DashStyle.Dot };
                    using var soft = new SolidBrush(Color.FromArgb(60, entry.Color));
                    g.FillEllipse(soft, dot);
                    g.DrawEllipse(pen, dot);
                }
                else
                {
                    using var brush = new SolidBrush(entry.Color);
                    g.FillEllipse(brush, dot);
                }

                TextRenderer.DrawText(g, entry.Text, textFont, text, Theme.Text,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
            }
        }
    }
}

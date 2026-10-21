using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>
    /// Oszlopdiagram (havi bontás): az oszlopok az első megjelenéskor felnőnek, egérrel az oszlop fölé állva
    /// megjelenik a részletes érték. A kártyán belül a teljes szélességet kitölti ("fill").
    /// </summary>
    internal sealed class BarChart : Control
    {
        internal readonly record struct Bar(string Label, double Value, string ValueText, string Tooltip);

        private readonly Color color;
        private List<Bar> bars = new();
        private double progress;
        private bool started;
        private int hover = -1;

        public BarChart(Color color)
        {
            this.color = color;
            Height = Ui.S(236);
            Margin = Ui.Pad(0, 4, 0, 0);
            Tag = "fill";
            AccessibleRole = AccessibleRole.Chart;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetBars(IEnumerable<Bar> data)
        {
            bars = data.ToList();
            AccessibleName = "Diagram: " + string.Join("; ", bars.Select(b => $"{b.Label}: {b.ValueText}"));
            Invalidate();
        }

        private Rectangle PlotArea => new(Ui.S(6), Ui.S(34), Math.Max(Width - Ui.S(12), 1), Math.Max(Height - Ui.S(34) - Ui.S(32), 1));

        private int IndexAt(Point location)
        {
            var plot = PlotArea;
            if (bars.Count == 0 || location.X < plot.Left || location.X >= plot.Right) return -1;

            int index = (int)((location.X - plot.Left) / (plot.Width / (double)bars.Count));
            return index >= 0 && index < bars.Count ? index : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = IndexAt(e.Location);
            if (index == hover) return;

            hover = index;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover < 0) return;

            hover = -1;
            Invalidate();
        }

        /// <summary>Csak a felső sarkok lekerekítettek (az oszlop alja a vízszintes vonalra ül).</summary>
        private static GraphicsPath TopRounded(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (d < 2)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // a növekedés az első tényleges megjelenéskor indul (a háttérben épülő, még takart oldalon nem)
            if (!started)
            {
                started = true;
                Anim.Run(700, t =>
                {
                    progress = t;
                    Invalidate();
                });
            }

            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            if (bars.Count == 0 || Width < Ui.S(40)) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var plot = PlotArea;
            double max = StatsLogic.NiceMax(bars.Max(b => b.Value));

            // halvány vízszintes segédvonalak
            using (var grid = new Pen(Theme.CardBorder))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = plot.Bottom - (int)(plot.Height * i / 4.0);
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                }
            }

            double slot = plot.Width / (double)bars.Count;
            int barWidth = (int)Math.Min(slot * 0.58, Ui.S(60));
            var labelFont = Ui.MakeFont(8.5F);
            var valueFont = Ui.MakeFont(8.5F, FontStyle.Bold);
            bool labelAll = bars.Count <= 13;

            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                int x = plot.Left + (int)(i * slot + (slot - barWidth) / 2);
                int height = bar.Value <= 0 ? 0 : Math.Max((int)(bar.Value / max * plot.Height * progress), Ui.S(3));
                var rect = new Rectangle(x, plot.Bottom - height, barWidth, height);

                bool hot = i == hover;
                if (height > 0)
                {
                    using var path = TopRounded(rect, Ui.S(5));
                    using var brush = new SolidBrush(hot ? ControlPaint.Dark(color, 0.12F) : Anim.Lerp(Theme.CardBg, color, 0.88));
                    g.FillPath(brush, path);
                }

                // érték az oszlop fölött (sok oszlopnál csak a kijelölt)
                if (bar.Value > 0 && bar.ValueText.Length > 0 && (labelAll || hot) && progress > 0.6)
                {
                    TextRenderer.DrawText(g, bar.ValueText, valueFont,
                        new Rectangle(x - Ui.S(20), rect.Y - Ui.S(20), barWidth + Ui.S(40), Ui.S(18)), Theme.Text,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                }

                // hónap a vonal alatt
                TextRenderer.DrawText(g, bar.Label, labelFont,
                    new Rectangle((int)(plot.Left + i * slot), plot.Bottom + Ui.S(8), (int)slot, Ui.S(20)),
                    hot ? Theme.Text : Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            // részletek az egér alatti oszlopnál
            if (hover >= 0 && hover < bars.Count && bars[hover].Tooltip.Length > 0)
                PaintTooltip(g, bars[hover].Tooltip, plot, hover, slot, max);
        }

        private void PaintTooltip(Graphics g, string text, Rectangle plot, int index, double slot, double max)
        {
            var font = Ui.MakeFont(9F, FontStyle.Bold);
            var size = TextRenderer.MeasureText(text, font);
            int width = size.Width + Ui.S(20);
            int height = size.Height + Ui.S(12);

            double value = bars[index].Value;
            int barTop = plot.Bottom - (int)(value / max * plot.Height * progress);
            int centerX = plot.Left + (int)(index * slot + slot / 2);
            int x = Math.Clamp(centerX - width / 2, 0, Math.Max(Width - width, 0));
            int y = Math.Max(barTop - height - Ui.S(26), 0);
            var box = new Rectangle(x, y, width, height);

            using (var path = Ui.RoundedPath(box, Ui.S(8)))
            using (var fill = new SolidBrush(Color.FromArgb(235, Theme.Dark ? Color.FromArgb(60, 66, 84) : Color.FromArgb(33, 37, 41))))
                g.FillPath(fill, path);
            TextRenderer.DrawText(g, text, font, box, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Vízszintes rangsor-sávok: név, arányos sáv és érték soronként (tantárgyak, diákok, oktatók szerint).</summary>
    internal sealed class RankBars : Control
    {
        internal readonly record struct Row(string Name, double Value, string ValueText);

        private readonly Color color;
        private readonly List<Row> rows = new();
        private double progress;
        private bool started;

        private static int RowHeight => Ui.S(38);

        public RankBars(Color color)
        {
            this.color = color;
            Margin = Ui.Pad(0, 4, 0, 0);
            Tag = "fill";
            AccessibleRole = AccessibleRole.Chart;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetRows(IEnumerable<Row> data)
        {
            rows.Clear();
            rows.AddRange(data);
            Height = Math.Max(rows.Count, 1) * RowHeight;
            AccessibleName = "Rangsor: " + string.Join("; ", rows.Select(r => $"{r.Name}: {r.ValueText}"));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!started)
            {
                started = true;
                Anim.Run(700, t =>
                {
                    progress = t;
                    Invalidate();
                });
            }

            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            if (rows.Count == 0 || Width < Ui.S(120)) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            double max = Math.Max(rows.Max(r => r.Value), 1e-9);
            int nameWidth = Math.Min(Ui.S(190), Width / 3);
            int textWidth = Math.Min(Ui.S(170), Width / 3);
            int barLeft = nameWidth + Ui.S(10);
            int barWidth = Math.Max(Width - barLeft - textWidth - Ui.S(12), Ui.S(20));
            int barHeight = Ui.S(14);

            var nameFont = Ui.MakeFont(10F, FontStyle.Bold);
            var valueFont = Ui.MakeFont(9.5F);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int top = i * RowHeight;
                int middle = top + RowHeight / 2;

                TextRenderer.DrawText(g, row.Name, nameFont, new Rectangle(0, top, nameWidth, RowHeight), Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                var track = new Rectangle(barLeft, middle - barHeight / 2, barWidth, barHeight);
                using (var trackPath = Ui.RoundedPath(track, barHeight / 2))
                using (var trackBrush = new SolidBrush(Theme.SoftHover))
                    g.FillPath(trackBrush, trackPath);

                int filled = row.Value <= 0 ? 0 : Math.Max((int)(barWidth * (row.Value / max) * progress), barHeight);
                if (filled > 0)
                {
                    var fill = new Rectangle(barLeft, track.Y, filled, barHeight);
                    using var fillPath = Ui.RoundedPath(fill, barHeight / 2);
                    using var fillBrush = new SolidBrush(color);
                    g.FillPath(fillBrush, fillPath);
                }

                TextRenderer.DrawText(g, row.ValueText, valueFont,
                    new Rectangle(barLeft + barWidth + Ui.S(12), top, textWidth, RowHeight), Theme.Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
        }
    }
}

using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>
    /// Kiemelt, lekerekített tájékoztató doboz ikonnal és tördelt szöveggel (kártyán belül a teljes szélességet kitölti).
    /// Info: kék árnyalatú (fontos tudnivaló), Muted: semleges (pl. a diák üzenete).
    /// </summary>
    internal sealed class NoticeBox : Control
    {
        internal enum Tone { Info, Muted }

        private readonly string glyph;
        private readonly Tone tone;
        private readonly string title;
        private readonly Font titleFont = Ui.MakeFont(10F, FontStyle.Bold);
        private readonly Font textFont = Ui.MakeFont(10F);

        private static int PadX => Ui.S(14);
        private static int PadY => Ui.S(10);
        private static int IconWidth => Ui.S(30);

        public NoticeBox(string glyph, string title, string text, Tone tone = Tone.Info)
        {
            this.glyph = glyph;
            this.tone = tone;
            this.title = title;
            Text = text;
            Tag = "fill"; // a CardPanel a belső szélességére húzza
            Margin = Ui.Pad(0, 4, 0, 8);
            AccessibleName = title.Length > 0 ? title + ": " + text : text;
            AccessibleRole = AccessibleRole.StaticText;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Ui.S(48);
        }

        private int TextWidth => Math.Max(Width - PadX * 2 - IconWidth, Ui.S(80));

        private const TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        private int MeasureHeight()
        {
            int height = PadY * 2;
            if (title.Length > 0) height += Ui.LineHeight(titleFont) + Ui.S(2);
            height += TextRenderer.MeasureText(Text, textFont, new Size(TextWidth, 0), Flags).Height;
            return Math.Max(height, Ui.S(44));
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            // a magasság a szélességhez igazodik (a szöveg tördelődik)
            if (Width <= 0) return;
            int wanted = MeasureHeight();
            if (Height != wanted) Height = wanted;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            if (Width < 20 || Height < 10) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool info = tone == Tone.Info;
            Color accent = info ? Theme.MainBlue : Theme.Muted;
            Color fill = info ? Anim.Lerp(Theme.CardBg, Theme.MainBlue, Theme.Dark ? 0.20 : 0.09) : Theme.SoftHover;
            Color border = info ? Anim.Lerp(Theme.CardBorder, Theme.MainBlue, 0.45) : Theme.CardBorder;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.RoundedPath(rect, Ui.S(12)))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            using (var iconBrush = new SolidBrush(accent))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                g.DrawString(glyph, Ui.IconFont(12F), iconBrush, new Rectangle(PadX / 2, PadY, IconWidth, Ui.S(24)), center);

            int x = PadX + IconWidth;
            int y = PadY;
            if (title.Length > 0)
            {
                TextRenderer.DrawText(g, title, titleFont, new Rectangle(x, y, TextWidth, Ui.LineHeight(titleFont)), info ? Theme.Text : Theme.Text,
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                y += Ui.LineHeight(titleFont) + Ui.S(2);
            }
            TextRenderer.DrawText(g, Text, textFont, new Rectangle(x, y, TextWidth, Math.Max(Height - y - PadY, 0)),
                info ? Theme.Text : Theme.Muted, Flags);
        }
    }
}

using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    internal enum PillStyle
    {
        /// <summary>Kitöltött, a fő művelet (pl. Elfogadás).</summary>
        Filled,

        /// <summary>Halvány, a szín árnyalatával kitöltött (másodlagos művelet).</summary>
        Soft,

        /// <summary>Csak vékony keret (kevésbé hangsúlyos művelet).</summary>
        Ghost
    }

    /// <summary>
    /// Lekerekített ("pirula") gomb opcionális ikonnal: egységes, jól olvasható műveletgombok a kártyákon.
    /// Hoverkor finoman átúszik, billentyűzettel (Enter, szóköz) is használható.
    /// </summary>
    internal sealed class PillButton : Control
    {
        private readonly string glyph;
        private readonly Color color;
        private readonly PillStyle style;
        private double hoverT;
        private double pressT;
        private Anim.Tween? hoverTween;

        public PillButton(string text, Color color, PillStyle style = PillStyle.Soft, string glyph = "")
        {
            Text = text;
            this.color = color;
            this.style = style;
            this.glyph = glyph;

            Font = Ui.MakeFont(10F, FontStyle.Bold);
            Height = Ui.S(42);
            Margin = Ui.Pad(0, 0, 10, 0);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, true);

            int textWidth = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, 0), TextFormatFlags.NoPadding).Width;
            Width = Ui.S(20) + (glyph.Length > 0 ? Ui.S(26) : 0) + textWidth + Ui.S(22);
        }

        private void HoverTo(double to)
        {
            hoverTween?.Cancel();
            double from = hoverT;
            hoverTween = Anim.Run(130, t =>
            {
                hoverT = from + (to - from) * t;
                Invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e) { HoverTo(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { HoverTo(0); pressT = 0; base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressT = 1; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressT = 0; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            double alpha = Enabled ? 1 : 0.45;
            Color fill, text, border;
            switch (style)
            {
                case PillStyle.Filled:
                    fill = Anim.Lerp(color, Color.Black, 0.12 * hoverT + 0.08 * pressT);
                    text = Theme.White;
                    border = fill;
                    break;
                case PillStyle.Soft:
                    fill = Anim.Lerp(Theme.CardBg, color, (Theme.Dark ? 0.20 : 0.11) + 0.10 * hoverT);
                    text = color;
                    border = fill;
                    break;
                default:
                    fill = Anim.Lerp(Theme.CardBg, Theme.SoftHover, hoverT);
                    text = Theme.Text;
                    border = Anim.Lerp(Theme.CardBorder, color, hoverT);
                    break;
            }

            if (alpha < 1)
            {
                fill = Anim.Lerp(Theme.CardBg, fill, alpha);
                text = Anim.Lerp(Theme.CardBg, text, alpha);
                border = Anim.Lerp(Theme.CardBg, border, alpha);
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.RoundedPath(rect, Height / 2))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, 1.2F))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            int x = Ui.S(20);
            if (glyph.Length > 0)
            {
                using var iconBrush = new SolidBrush(text);
                using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(glyph, glyph[0] < '' ? Ui.SymbolFont(11F) : Ui.IconFont(11F), iconBrush,
                    new Rectangle(x - Ui.S(4), 0, Ui.S(24), Height), center);
                x += Ui.S(26);
            }

            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Math.Max(Width - x - Ui.S(14), 0), Height), text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues) Ui.DrawFocusRing(g, rect, Height / 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) hoverTween?.Cancel();
            base.Dispose(disposing);
        }
    }
}

using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace TutorConnectApp
{
    /// <summary>
    /// Beírható számmező a szűrőkhöz (óradíj, értékelés): kis felirat felül, alatta a beírt érték és a mértékegység.
    /// Ugyanolyan megjelenésű, mint a legördülő mezők. Üresen: nincs korlát. Magyar és angol tizedesjelet is elfogad.
    /// </summary>
    internal sealed class NumberField : Control
    {
        private readonly string caption;
        private readonly string unit;
        private readonly bool decimals;
        private readonly decimal minimum;
        private readonly decimal maximum;
        private readonly TextBox input;
        private double focusT;
        private double hoverT;
        private Anim.Tween? focusTween;
        private Anim.Tween? hoverTween;
        private bool invalid;

        public event EventHandler? ValueChanged;

        /// <param name="unit">A mértékegység a mező jobb szélén (pl. "Ft", "★").</param>
        public NumberField(string caption, string placeholder, string unit, decimal minimum, decimal maximum, bool decimals = false)
        {
            this.caption = caption;
            this.unit = unit;
            this.minimum = minimum;
            this.maximum = maximum;
            this.decimals = decimals;

            Size = Ui.Sz(250, 58);
            Margin = Ui.Pad(0, 0, 14, 14);
            Cursor = Cursors.IBeam;
            AccessibleRole = AccessibleRole.Text;
            AccessibleName = caption;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            input = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Ui.MakeFont(10.5F, FontStyle.Bold),
                PlaceholderText = placeholder,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                MaxLength = decimals ? 5 : 7,
                AccessibleName = caption + " (írd be az értéket)"
            };
            input.GotFocus += (_, _) => FocusTo(1);
            input.LostFocus += (_, _) =>
            {
                FocusTo(0);
                Normalize();
            };
            input.TextChanged += (_, _) =>
            {
                invalid = input.TextLength > 0 && TryParse(input.Text) == null;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };
            input.KeyPress += (_, e) =>
            {
                // csak számjegy (és tizedesjel, ha engedett); a vezérlőbillentyűk (törlés, másolás) maradnak
                if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar)) return;
                if (decimals && (e.KeyChar == ',' || e.KeyChar == '.') && !input.Text.Contains(',') && !input.Text.Contains('.')) return;
                e.Handled = true;
            };
            input.MouseEnter += (_, _) => HoverTo(1);
            input.MouseLeave += (_, _) => HoverTo(0);
            Controls.Add(input);
            LayoutInput();
        }

        /// <summary>A beírt szám; null, ha a mező üres vagy érvénytelen.</summary>
        public decimal? Value => TryParse(input.Text);

        /// <summary>A mező szövege (állapot mentéséhez).</summary>
        public string RawText => input.Text;

        public void SetText(string text)
        {
            input.Text = text;
            invalid = false;
        }

        public void FocusInput() => input.Focus();

        private decimal? TryParse(string text)
        {
            text = text.Trim().Replace(',', '.');
            if (text.Length == 0) return null;
            if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) return null;
            return value < minimum || value > maximum ? null : value;
        }

        /// <summary>A mező elhagyásakor az érvénytelen (határon kívüli) érték a legközelebbi megengedettre javul.</summary>
        private void Normalize()
        {
            string raw = input.Text.Trim().Replace(',', '.');
            if (raw.Length == 0) return;
            if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) return;

            decimal clamped = Math.Clamp(value, minimum, maximum);
            if (clamped != value)
                input.Text = decimals ? clamped.ToString("0.#", Ui.Hu) : clamped.ToString("0");
        }

        private void FocusTo(double to)
        {
            focusTween?.Cancel();
            double from = focusT;
            focusTween = Anim.Run(160, t =>
            {
                focusT = from + (to - from) * t;
                Invalidate();
            });
        }

        private void HoverTo(double to)
        {
            hoverTween?.Cancel();
            double from = hoverT;
            hoverTween = Anim.Run(140, t =>
            {
                hoverT = from + (to - from) * t;
                Invalidate();
            });
        }

        private void LayoutInput()
        {
            if (input is null) return;

            int unitWidth = unit.Length == 0 ? 0 : TextRenderer.MeasureText(unit, Ui.MakeFont(10F, FontStyle.Bold)).Width + Ui.S(14);
            input.Left = Ui.S(18);
            input.Width = Math.Max(Width - Ui.S(18) - Ui.S(14) - unitWidth, 20);
            input.Top = Ui.S(27);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutInput();
        }

        protected override void OnMouseEnter(EventArgs e) { HoverTo(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { HoverTo(0); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); input.Focus(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            double highlight = Math.Max(focusT, hoverT * 0.7);
            Color borderColor = invalid ? Theme.Danger : Anim.Lerp(Theme.CardBorder, Theme.MainBlue, highlight);
            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(16)))
            using (var fill = new SolidBrush(Theme.InputBg))
            using (var border = new Pen(borderColor, 1F + (float)focusT))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            TextRenderer.DrawText(g, caption, Ui.MakeFont(8.5F),
                new Rectangle(Ui.S(18), Ui.S(8), Math.Max(Width - Ui.S(36), 0), Ui.S(17)),
                invalid ? Theme.Danger : Theme.Muted,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (unit.Length > 0)
            {
                TextRenderer.DrawText(g, unit, Ui.MakeFont(10F, FontStyle.Bold),
                    new Rectangle(Width - Ui.S(14) - Ui.S(60), Ui.S(26), Ui.S(60), Ui.S(24)),
                    Anim.Lerp(Theme.Muted, Theme.MainBlue, focusT),
                    TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                focusTween?.Cancel();
                hoverTween?.Cancel();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Jelölőnégyzet lekerekített "pirulában": kis négyzet pipával, mellette a felirat (opcionális ikonnal).
    /// Kattintásra, szóközre és Enterre vált; a pipa és a háttér finoman átúszik.
    /// </summary>
    internal sealed class CheckPill : Control
    {
        private readonly string glyph;
        private bool isChecked;
        private double checkT;
        private double hoverT;
        private Anim.Tween? checkTween;
        private Anim.Tween? hoverTween;

        public event EventHandler? CheckedChanged;

        public CheckPill(string text, string glyph = "", bool initial = false)
        {
            this.glyph = glyph;
            Text = text;
            isChecked = initial;
            checkT = initial ? 1 : 0;

            Font = Ui.MakeFont(10.5F, FontStyle.Bold);
            Height = Ui.S(58);
            Margin = Ui.Pad(0, 0, 14, 14);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
            AccessibleName = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, true);

            int textWidth = TextRenderer.MeasureText(text, Font).Width;
            int iconWidth = glyph.Length > 0 ? Ui.S(30) : 0;
            Width = Ui.S(18) + Ui.S(28) + Ui.S(12) + iconWidth + textWidth + Ui.S(22);
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => isChecked;
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                Animate();
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new PillAccessibility(this);

        private sealed class PillAccessibility : ControlAccessibleObject
        {
            private readonly CheckPill owner;

            public PillAccessibility(CheckPill owner) : base(owner) => this.owner = owner;

            public override AccessibleStates State => base.State | (owner.isChecked ? AccessibleStates.Checked : AccessibleStates.None);

            public override string DefaultAction => owner.isChecked ? "Kikapcsolás" : "Bekapcsolás";

            public override void DoDefaultAction() => owner.Checked = !owner.isChecked;
        }

        private void Animate()
        {
            checkTween?.Cancel();
            double from = checkT;
            double to = isChecked ? 1 : 0;
            checkTween = Anim.Run(180, t =>
            {
                checkT = from + (to - from) * t;
                Invalidate();
            });
        }

        private void HoverTo(double to)
        {
            hoverTween?.Cancel();
            double from = hoverT;
            hoverTween = Anim.Run(140, t =>
            {
                hoverT = from + (to - from) * t;
                Invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e) { HoverTo(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { HoverTo(0); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !isChecked; }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Space or Keys.Enter)
            {
                e.Handled = true;
                Checked = !isChecked;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Anim.Lerp(Theme.InputBg, Anim.Lerp(Theme.CardBg, Theme.MainBlue, Theme.Dark ? 0.20 : 0.09), checkT);
            Color border = Anim.Lerp(Anim.Lerp(Theme.CardBorder, Theme.MainBlue, hoverT * 0.7), Theme.MainBlue, checkT);

            using (var path = Ui.RoundedPath(rect, Ui.S(16)))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, 1F + (float)checkT))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            // jelölőnégyzet
            int box = Ui.S(26);
            var boxRect = new Rectangle(Ui.S(18), (Height - box) / 2, box, box);
            using (var boxPath = Ui.RoundedPath(boxRect, Ui.S(8)))
            {
                using var boxFill = new SolidBrush(Anim.Lerp(Theme.CardBg, Theme.MainBlue, checkT));
                using var boxBorder = new Pen(Anim.Lerp(Theme.Muted, Theme.MainBlue, checkT), Ui.S(1.6F));
                g.FillPath(boxFill, boxPath);
                g.DrawPath(boxBorder, boxPath);
            }

            if (checkT > 0.05)
            {
                // pipa, rajzolva: a hossza az animációval nő
                using var tick = new Pen(Color.FromArgb((int)(255 * Math.Min(checkT * 1.5, 1)), Color.White), Ui.S(2.6F))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                var a = new PointF(boxRect.X + box * 0.24F, boxRect.Y + box * 0.54F);
                var b = new PointF(boxRect.X + box * 0.43F, boxRect.Y + box * 0.72F);
                var c = new PointF(boxRect.X + box * 0.76F, boxRect.Y + box * 0.32F);
                float t = (float)checkT;
                g.DrawLine(tick, a, new PointF(a.X + (b.X - a.X) * Math.Min(t * 2, 1), a.Y + (b.Y - a.Y) * Math.Min(t * 2, 1)));
                if (t > 0.5F)
                {
                    float u = (t - 0.5F) * 2;
                    g.DrawLine(tick, b, new PointF(b.X + (c.X - b.X) * u, b.Y + (c.Y - b.Y) * u));
                }
            }

            int x = boxRect.Right + Ui.S(12);
            if (glyph.Length > 0)
            {
                using var iconBrush = new SolidBrush(Anim.Lerp(Theme.Muted, Theme.Danger, checkT));
                using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(glyph, glyph[0] < '\uE000' ? Ui.SymbolFont(13F) : Ui.IconFont(12F), iconBrush, new Rectangle(x, 0, Ui.S(24), Height), center);
                x += Ui.S(30);
            }

            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Math.Max(Width - x - Ui.S(10), 0), Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (Focused && ShowFocusCues) Ui.DrawFocusRing(g, rect, Ui.S(16));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                checkTween?.Cancel();
                hoverTween?.Cancel();
            }
            base.Dispose(disposing);
        }
    }
}

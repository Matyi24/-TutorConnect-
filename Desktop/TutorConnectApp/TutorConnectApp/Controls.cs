using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>Lapos gomb, amelynek a háttere finoman átúszik hoverkor.</summary>
    internal class HoverButton : Button
    {
        private Color normal = Theme.MainBlue;
        private Color hover = Theme.MainBlueDark;
        private bool hovering;
        private Anim.Tween? tween;

        public HoverButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Font = Ui.MakeFont(9.5F, FontStyle.Bold);
            Padding = Ui.Pad(14, 5, 14, 5);
            Margin = Ui.Pad(0, 6, 8, 0);
            Cursor = Cursors.Hand;
            ForeColor = Theme.White;
            Apply(normal);
        }

        public void SetColors(Color normalColor, Color hoverColor, Color? border)
        {
            normal = normalColor;
            hover = hoverColor;
            if (border is Color borderColor)
            {
                FlatAppearance.BorderSize = 1;
                FlatAppearance.BorderColor = borderColor;
            }
            else
            {
                FlatAppearance.BorderSize = 0;
            }
            Apply(hovering ? hover : normal);
        }

        private void Apply(Color color)
        {
            BackColor = color;
            FlatAppearance.MouseOverBackColor = color;
            FlatAppearance.MouseDownBackColor = color;
        }

        private void GoTo(Color target)
        {
            tween?.Cancel();
            Color from = BackColor;
            tween = Anim.Run(140, t => Apply(Anim.Lerp(from, target, t)));
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovering = true;
            GoTo(hover);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovering = false;
            GoTo(normal);
            base.OnMouseLeave(e);
        }

        // az AcceptButton ne kapjon külön "alapértelmezett gomb" keretet
        public override void NotifyDefault(bool value) => base.NotifyDefault(false);

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            // letiltva halványabb háttér
            Apply(Enabled ? (hovering ? hover : normal) : Anim.Lerp(normal, Theme.CardBg, 0.55));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tween?.Cancel();
            base.Dispose(disposing);
        }
    }

    /// <summary>Kiválasztható "pirula" (fülek, tantárgy- és időpontválasztó). A kijelölés animálva vált.</summary>
    internal class SelectableChip : Control
    {
        private bool selected;
        private double selectedT;
        private double hoverT;
        private Anim.Tween? selectTween;
        private Anim.Tween? hoverTween;

        public SelectableChip(string text)
        {
            Text = text;
            Font = Ui.MakeFont(10F, FontStyle.Bold);
            Height = Ui.S(38);
            Margin = Ui.Pad(0, 0, 10, 10);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, true); // billentyűzettel is kijelölhető
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
            AccessibleName = text;
            UpdateWidth();
        }

        public bool IsSelected => selected;

        public void SetText(string text)
        {
            Text = text;
            AccessibleName = text;
            UpdateWidth();
            Invalidate();
        }

        public void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value;
            AccessibleDescription = value ? "kijelölve" : "nincs kijelölve";

            selectTween?.Cancel();
            double from = selectedT;
            double to = value ? 1 : 0;
            selectTween = Anim.Run(180, t =>
            {
                selectedT = from + (to - from) * t;
                Invalidate();
            });
        }

        private void UpdateWidth() => Width = TextRenderer.MeasureText(Text, Font).Width + Ui.S(36);

        private void AnimateHover(double to)
        {
            hoverTween?.Cancel();
            double from = hoverT;
            hoverTween = Anim.Run(140, t =>
            {
                hoverT = from + (to - from) * t;
                Invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e) { AnimateHover(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { AnimateHover(0); base.OnMouseLeave(e); }

        /// <summary>Billentyűzetes használat: Enter vagy szóköz kattintásnak számít.</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
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

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Anim.Lerp(Theme.CardBg, Theme.MainBlue, selectedT);
            Color border = Anim.Lerp(Anim.Lerp(Theme.CardBorder, Theme.MainBlue, hoverT), Theme.MainBlue, selectedT);
            Color text = Anim.Lerp(Theme.Text, Theme.White, selectedT);

            using var path = Ui.RoundedPath(rect, Height / 2);
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, path);
            using (var pen = new Pen(border, Ui.S(1.5F)))
                g.DrawPath(pen, path);

            TextRenderer.DrawText(g, Text, Font, rect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues) Ui.DrawFocusRing(g, rect, Height / 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                selectTween?.Cancel();
                hoverTween?.Cancel();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Kapcsoló (be/ki), a gomb animálva csúszik.</summary>
    internal class ToggleSwitch : Control
    {
        private bool on;
        private double position;
        private Anim.Tween? tween;

        public event EventHandler? Toggled;

        public ToggleSwitch(bool initial)
        {
            on = initial;
            position = initial ? 1 : 0;
            Size = Ui.Sz(52, 28);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, true); // billentyűzettel is kapcsolható (szóköz)
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        public bool IsOn => on;

        protected override AccessibleObject CreateAccessibilityInstance() => new SwitchAccessibility(this);

        /// <summary>A képernyőolvasó a kapcsoló állapotát (be / ki) is bemondja.</summary>
        private sealed class SwitchAccessibility : ControlAccessibleObject
        {
            private readonly ToggleSwitch owner;

            public SwitchAccessibility(ToggleSwitch owner) : base(owner) => this.owner = owner;

            public override AccessibleStates State => base.State | (owner.on ? AccessibleStates.Checked : AccessibleStates.None);

            public override string DefaultAction => owner.on ? "Kikapcsolás" : "Bekapcsolás";

            public override void DoDefaultAction() => owner.OnClick(EventArgs.Empty);
        }

        /// <summary>Billentyűzetes használat: Enter vagy szóköz kattintásnak számít.</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            on = !on;
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);

            tween?.Cancel();
            double from = position;
            double to = on ? 1 : 0;
            tween = Anim.Run(200, t =>
            {
                position = from + (to - from) * t;
                Invalidate();
            });

            Toggled?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color off = Theme.Dark ? Theme.SwitchOffDark : Theme.SwitchOffLight;
            using var track = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2);
            using (var brush = new SolidBrush(Anim.Lerp(off, Theme.MainBlue, position)))
                g.FillPath(brush, track);

            int margin = Ui.S(4);
            int d = Height - margin * 2;
            int x = margin + (int)((Width - d - margin * 2) * position);
            using var knob = new SolidBrush(Theme.White);
            g.FillEllipse(knob, x, margin, d, d);

            if (Focused && ShowFocusCues) Ui.DrawFocusRing(g, new Rectangle(0, 0, Width - 1, Height - 1), Height / 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tween?.Cancel();
            base.Dispose(disposing);
        }
    }

    /// <summary>Forgó töltésjelző.</summary>
    internal class Spinner : Control
    {
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 30 };
        private float angle;

        public Spinner()
        {
            Size = Ui.Sz(26, 26);
            Margin = Ui.Pad(0);
            AccessibleName = "Betöltés";
            AccessibleRole = AccessibleRole.Animation;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            timer.Tick += (_, _) =>
            {
                angle = (angle + 20) % 360;
                Invalidate();
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            timer.Start();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            timer.Stop();
            base.OnHandleDestroyed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int inset = Ui.S(3);
            var rect = new Rectangle(inset, inset, Width - inset * 2 - 1, Height - inset * 2 - 1);

            using (var track = new Pen(Theme.CardBorder, Ui.S(3F)))
                g.DrawEllipse(track, rect);
            using var arc = new Pen(Theme.MainBlue, Ui.S(3F)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(arc, rect, angle, 100);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Ötcsillagos értékelő.</summary>
    internal class StarRating : Control
    {
        private static int StarSize => Ui.S(46);
        private int value;
        private int hoverValue;

        public event EventHandler? ValueChanged;

        public StarRating()
        {
            Size = new Size(StarSize * 5, StarSize);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, true); // nyilakkal vagy 1–5 számmal is állítható
            TabStop = true;
            AccessibleName = "Értékelés csillagokban";
            AccessibleRole = AccessibleRole.Slider;
        }

        public int Value => value;

        private void SetValue(int stars)
        {
            stars = Math.Clamp(stars, 1, 5);
            if (stars == value) return;

            value = stars;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new StarAccessibility(this);

        private sealed class StarAccessibility : ControlAccessibleObject
        {
            private readonly StarRating owner;

            public StarAccessibility(StarRating owner) : base(owner) => this.owner = owner;

            public override string Value => owner.value == 0 ? "nincs kiválasztva" : $"{owner.value} csillag az ötből";
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Right or Keys.Up:
                    SetValue(value + 1);
                    e.Handled = true;
                    break;
                case Keys.Left or Keys.Down:
                    SetValue(value - 1);
                    e.Handled = true;
                    break;
                case >= Keys.D1 and <= Keys.D5:
                    SetValue(e.KeyCode - Keys.D0);
                    e.Handled = true;
                    break;
                case >= Keys.NumPad1 and <= Keys.NumPad5:
                    SetValue(e.KeyCode - Keys.NumPad0);
                    e.Handled = true;
                    break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        private static int StarAt(int x) => Math.Clamp(x / StarSize + 1, 1, 5);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            hoverValue = StarAt(e.X);
            Invalidate();
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverValue = 0;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            SetValue(StarAt(e.X));
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int lit = hoverValue > 0 ? hoverValue : value;
            var font = Ui.SymbolFont(26F);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            for (int i = 1; i <= 5; i++)
            {
                Color color = i <= lit ? Theme.Star : Theme.CardBorder;
                using var brush = new SolidBrush(color);
                g.DrawString("★", font, brush, new Rectangle((i - 1) * StarSize, 0, StarSize, StarSize), format);
            }

            if (Focused && ShowFocusCues) Ui.DrawFocusRing(g, new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(10));
        }
    }

    /// <summary>Kör alakú avatar a név kezdőbetűivel.</summary>
    internal class AvatarCircle : Control
    {
        private readonly string initials;

        public AvatarCircle(string name, int size)
        {
            initials = Ui.Initials(name);
            Size = new Size(Ui.S(size), Ui.S(size));
            Margin = Ui.Pad(0, 0, 14, 0);
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Ui.DrawAvatar(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), initials, Theme.MainBlue, Theme.White);
        }
    }
}

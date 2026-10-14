using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    // Saját, animált vezérlők. Mindegyik a Theme aktuális színeit használja.

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
            Padding = new Padding(14, 5, 14, 5);
            Margin = new Padding(0, 6, 8, 0);
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
            Height = 38;
            Margin = new Padding(0, 0, 10, 10);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            UpdateWidth();
        }

        public bool IsSelected => selected;

        public void SetText(string text)
        {
            Text = text;
            UpdateWidth();
            Invalidate();
        }

        public void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value;

            selectTween?.Cancel();
            double from = selectedT;
            double to = value ? 1 : 0;
            selectTween = Anim.Run(180, t =>
            {
                selectedT = from + (to - from) * t;
                Invalidate();
            });
        }

        private void UpdateWidth() => Width = TextRenderer.MeasureText(Text, Font).Width + 36;

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
            using (var pen = new Pen(border, 1.5F))
                g.DrawPath(pen, path);

            TextRenderer.DrawText(g, Text, Font, rect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
            Size = new Size(52, 28);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        public bool IsOn => on;

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            on = !on;

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

            int d = Height - 8;
            int x = 4 + (int)((Width - d - 8) * position);
            using var knob = new SolidBrush(Theme.White);
            g.FillEllipse(knob, x, 4, d, d);
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
            Size = new Size(26, 26);
            Margin = new Padding(0);
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
            var rect = new Rectangle(3, 3, Width - 7, Height - 7);

            using (var track = new Pen(Theme.CardBorder, 3F))
                g.DrawEllipse(track, rect);
            using var arc = new Pen(Theme.MainBlue, 3F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(arc, rect, angle, 100);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Számláló csempe: ikon, nagy szám (felszámlálós animációval) és felirat.</summary>
    internal class StatTile : Control
    {
        private readonly int target;
        private readonly string suffix;
        private readonly string caption;
        private readonly string glyph;
        private readonly Color accent;
        private double progress;
        private bool started;

        public StatTile(int value, string suffix, string caption, string glyph, Color accent)
        {
            target = value;
            this.suffix = suffix;
            this.caption = caption;
            this.glyph = glyph;
            this.accent = accent;
            Size = new Size(232, 104);
            Margin = new Padding(0, 0, 16, 16);
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && !started)
            {
                started = true;
                Anim.Run(900, t =>
                {
                    progress = t;
                    Invalidate();
                });
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 14))
            {
                using var fill = new SolidBrush(Theme.CardBg);
                using var border = new Pen(Theme.CardBorder);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            // ikon körben
            var badge = new Rectangle(20, (Height - 46) / 2, 46, 46);
            using (var badgeBrush = new SolidBrush(Anim.Lerp(Theme.CardBg, accent, Theme.Dark ? 0.28 : 0.15)))
                g.FillEllipse(badgeBrush, badge);
            var iconFont = Ui.IconFont(16F);
            using (var iconBrush = new SolidBrush(accent))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, iconFont, iconBrush, badge, center);

            int shown = (int)Math.Round(target * progress);
            int textX = badge.Right + 16;
            int textWidth = Math.Max(Width - textX - 10, 0);

            var valueFont = Ui.MakeFont(22F, FontStyle.Bold);
            using var valueBrush = new SolidBrush(Theme.Text);
            var captionFont = Ui.MakeFont(9.5F);
            using var captionBrush = new SolidBrush(Theme.Muted);
            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };

            g.DrawString(shown + suffix, valueFont, valueBrush, new Rectangle(textX, 18, textWidth, 40), format);
            g.DrawString(caption, captionFont, captionBrush, new Rectangle(textX, 60, textWidth, 24), format);
        }
    }

    /// <summary>Színátmenetes üdvözlő sáv a főoldal tetején, opcionális gombbal.</summary>
    internal class HeroBanner : Control
    {
        private readonly string title;
        private readonly string subtitle;

        public HeroBanner(string title, string subtitle, string? buttonText, Action? onButton)
        {
            this.title = title;
            this.subtitle = subtitle;
            Height = 150;
            Margin = new Padding(0, 0, 0, 20);
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            if (buttonText != null)
            {
                var button = Ui.Btn(buttonText, Theme.White, Theme.MainBlueDark, (_, _) => onButton?.Invoke());
                button.Location = new Point(28, 100);
                Controls.Add(button);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            if (Width < 10 || Height < 10) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color c1 = Theme.Dark ? Theme.HeroDark1 : Theme.HeroLight1;
            Color c2 = Theme.Dark ? Theme.HeroDark2 : Theme.MainBlueDark;

            using var path = Ui.RoundedPath(rect, 18);
            using (var brush = new LinearGradientBrush(rect, c1, c2, 20F))
                g.FillPath(brush, path);

            // díszítő körök
            g.SetClip(path);
            using (var circle = new SolidBrush(Color.FromArgb(34, Color.White)))
            {
                g.FillEllipse(circle, Width - 190, -60, 260, 260);
                g.FillEllipse(circle, Width - 90, 70, 150, 150);
            }
            g.ResetClip();

            var titleFont = Ui.MakeFont(21F, FontStyle.Bold);
            var subFont = Ui.MakeFont(10.5F);
            using var white = new SolidBrush(Color.White);
            using var soft = new SolidBrush(Color.FromArgb(225, Color.White));
            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };

            int textWidth = Math.Max(Width - 56, 0);
            g.DrawString(title, titleFont, white, new Rectangle(28, 24, textWidth, 40), format);
            g.DrawString(subtitle, subFont, soft, new Rectangle(28, 66, textWidth, 28), format);
        }
    }

    /// <summary>Ötcsillagos értékelő.</summary>
    internal class StarRating : Control
    {
        private const int StarSize = 46;
        private int value;
        private int hoverValue;

        public event EventHandler? ValueChanged;

        public StarRating()
        {
            Size = new Size(StarSize * 5, StarSize);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public int Value => value;

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
            value = StarAt(e.X);
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
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
        }
    }

    /// <summary>
    /// Lekerekített kártya: a tartalma függőlegesen egymás alá kerül, a magassága automatikus.
    /// A SetReveal a megjelenési animációhoz (halványodó háttér) kell.
    /// </summary>
    internal class CardPanel : FlowLayoutPanel
    {
        private double reveal = 1;
        private GraphicsPath? outline;

        public CardPanel()
        {
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Theme.CardBg;
            Padding = new Padding(22, 18, 22, 18);
            Margin = new Padding(0, 0, 0, 16);
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        public void SetReveal(double value)
        {
            reveal = Math.Clamp(value, 0, 1);
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));

            // az átlátszó (belső) paneleken nincs saját kártya-rajz
            if (BackColor.A < 255 || Width < 4 || Height < 4) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            outline ??= Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 14);
            int alpha = (int)(255 * reveal);
            using (var fill = new SolidBrush(Color.FromArgb(alpha, BackColor)))
                g.FillPath(fill, outline);
            using (var border = new Pen(Color.FromArgb(alpha, Theme.CardBorder)))
                g.DrawPath(border, outline);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) outline?.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            outline?.Dispose(); // a körvonal a következő rajzoláskor az új mérettel épül újra
            outline = null;

            // a "wrap" jelölésű elemek a kártya belső szélességéhez igazodnak (hosszú szöveg és címkesorok tördelése)
            int inner = Math.Max(ClientSize.Width - Padding.Horizontal, 0);
            if (inner == 0) return;

            foreach (Control child in Controls)
            {
                string? tag = child.Tag as string;
                if (tag == "wrap")
                    child.MaximumSize = new Size(inner, 0);
                else if (tag == "fill")
                    child.Width = inner; // rögzített magasságú elem, a teljes belső szélességre húzva
            }
        }
    }

    /// <summary>
    /// Lekerekített felület (bejelentkező kártya). A panel átlátszó, így a sarkoknál a szülő háttere
    /// (pl. a színátmenet) látszik, a kártya színét maga rajzolja.
    /// </summary>
    internal class SurfacePanel : Panel
    {
        public SurfacePanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e); // a szülő hátterét rajzolja (átlátszó panel)
            if (Width < 4 || Height < 4) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 18);
            using var fill = new SolidBrush(Theme.CardBg);
            using var border = new Pen(Theme.CardBorder);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }
    }

    /// <summary>
    /// Tantárgy-kártya: kategória-színű szimbólum, név, kategória, oktatók száma és "megnézem" nyíl.
    /// Hoverkor animáltan kiemelkedik; csak akkor kattintható, ha az adott felhasználó látja az oktatókat.
    /// </summary>
    internal class SubjectCard : Control
    {
        private readonly string name;
        private readonly string category;
        private readonly string glyph;
        private readonly string tutorText;
        private readonly bool hasTutors;
        private readonly bool clickable;
        private readonly Color accent;
        private double hoverT;
        private Anim.Tween? hoverTween;

        public SubjectCard(SubjectDto subject, int tutorCount, bool clickable)
        {
            name = subject.Name;
            category = string.IsNullOrWhiteSpace(subject.Category) ? "Tantárgy" : subject.Category;
            glyph = SubjectGlyph(subject.Name);
            accent = Ui.CategoryColor(subject.Category);
            hasTutors = tutorCount > 0;
            tutorText = tutorCount switch
            {
                0 => "Jelenleg nincs oktató",
                1 => "1 elérhető oktató",
                _ => $"{tutorCount} elérhető oktató"
            };
            this.clickable = clickable;

            Size = new Size(330, 134);
            Margin = new Padding(0, 0, 18, 18);
            Cursor = clickable ? Cursors.Hand : Cursors.Default;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        /// <summary>A weboldal szimbólumai (vsc/js/index.js getSubjectIcon); ismeretlen tárgynál könyv-ikon.</summary>
        private static string SubjectGlyph(string subjectName)
        {
            string text = subjectName.ToLowerInvariant();
            (string[] Keywords, string Glyph)[] map =
            {
                (new[] { "matematika" }, "∑"),
                (new[] { "fizika" }, "⚛"),
                (new[] { "kémia" }, "⚗"),
                (new[] { "informatika", "programoz" }, "</>"),
                (new[] { "statisztika" }, "▥"),
                (new[] { "történelem" }, "◈"),
                (new[] { "magyar" }, "A"),
                (new[] { "filozófia" }, "Φ"),
                (new[] { "etika" }, "◆"),
                (new[] { "pszichológia" }, "Ψ"),
                (new[] { "társadalomismeret" }, "◉"),
                (new[] { "jog" }, "§"),
                (new[] { "angol" }, "EN"),
                (new[] { "német" }, "DE"),
                (new[] { "francia" }, "FR"),
                (new[] { "spanyol" }, "ES"),
                (new[] { "olasz" }, "IT"),
                (new[] { "orosz" }, "RU"),
                (new[] { "latin" }, "LA")
            };

            foreach (var (keywords, symbol) in map)
            {
                if (keywords.Any(text.Contains)) return symbol;
            }
            return "\uE8F1";
        }

        private void HoverTo(double to)
        {
            if (!clickable) return;
            hoverTween?.Cancel();
            double from = hoverT;
            hoverTween = Anim.Run(180, t =>
            {
                hoverT = from + (to - from) * t;
                Invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e) { HoverTo(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { HoverTo(0); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 18))
            {
                using var fill = new SolidBrush(Theme.CardBg);
                using var border = new Pen(Anim.Lerp(Theme.CardBorder, accent, hoverT), 1F + (float)hoverT);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            // kategória-színű szimbólum-négyzet
            var badge = new Rectangle(20, 20, 56, 56);
            Color tint = Anim.Lerp(Theme.CardBg, accent, Theme.Dark ? 0.30 : 0.15);
            using (var badgePath = Ui.RoundedPath(badge, 16))
            using (var badgeBrush = new SolidBrush(Anim.Lerp(tint, accent, hoverT)))
                g.FillPath(badgeBrush, badgePath);

            bool symbolFont = glyph[0] < '\uE000'; // a Segoe MDL2 ikonok a privát Unicode-tartományban vannak
            var glyphFont = symbolFont
                ? Ui.SymbolFont(glyph.Length >= 2 ? 13F : 20F, FontStyle.Bold)
                : Ui.IconFont(20F);
            using (var glyphBrush = new SolidBrush(Anim.Lerp(accent, Theme.White, hoverT)))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, glyphFont, glyphBrush, badge, center);

            int textX = badge.Right + 16;
            int textWidth = Math.Max(Width - textX - 18, 0);
            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };

            var nameFont = Ui.MakeFont(13.5F, FontStyle.Bold);
            using (var nameBrush = new SolidBrush(Theme.Text))
                g.DrawString(name, nameFont, nameBrush, new Rectangle(textX, 22, textWidth, 28), format);

            // kategória színes ponttal
            using (var dotBrush = new SolidBrush(accent))
                g.FillEllipse(dotBrush, textX, 59, 8, 8);
            var categoryFont = Ui.MakeFont(9.5F);
            using (var categoryBrush = new SolidBrush(Theme.Muted))
                g.DrawString(category, categoryFont, categoryBrush, new Rectangle(textX + 14, 52, Math.Max(textWidth - 14, 0), 22), format);

            // elválasztó
            using (var line = new Pen(Color.FromArgb(120, Theme.CardBorder)))
                g.DrawLine(line, 20, 90, Width - 20, 90);

            // alsó sor: oktatók száma, jobbra "Megnézem →"
            int arrowWidth = clickable ? 96 : 0;
            var pillFont = Ui.MakeFont(8.5F, FontStyle.Bold);
            {
                Color pillColor = hasTutors ? Theme.Success : Theme.Muted;
                int pillWidth = Math.Min(TextRenderer.MeasureText(tutorText, pillFont).Width + 8, Math.Max(Width - 40 - arrowWidth, 0));
                var pill = new Rectangle(20, 100, pillWidth, 24);
                using var pillPath = Ui.RoundedPath(pill, 12);
                using var pillBrush = new SolidBrush(Anim.Lerp(Theme.CardBg, pillColor, Theme.Dark ? 0.28 : 0.15));
                using var pillText = new SolidBrush(pillColor);
                using var pillFormat = new StringFormat(StringFormatFlags.NoWrap)
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                g.FillPath(pillBrush, pillPath);
                g.DrawString(tutorText, pillFont, pillText, pill, pillFormat);
            }

            if (clickable)
            {
                var moreFont = Ui.MakeFont(9.5F, FontStyle.Bold);
                using var moreBrush = new SolidBrush(Anim.Lerp(Theme.Muted, accent, hoverT));
                using var moreFormat = new StringFormat(StringFormatFlags.NoWrap)
                {
                    Alignment = StringAlignment.Far,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("Megnézem  →", moreFont, moreBrush, new Rectangle(Width - 20 - 110 + (int)(6 * hoverT), 100, 110, 24), moreFormat);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) hoverTween?.Cancel();
            base.Dispose(disposing);
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

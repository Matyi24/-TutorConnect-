using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>Számláló csempe: ikon, nagy szám (felszámlálós animációval) és felirat.</summary>
    internal class StatTile : Control
    {
        private readonly int target;
        private readonly string suffix;
        private readonly string caption;
        private readonly string glyph;
        private readonly Color accent;
        private readonly Font valueFont;
        private readonly Font captionFont;
        private readonly int valueHeight;
        private readonly int captionHeight;
        private double progress;
        private bool started;

        public StatTile(int value, string suffix, string caption, string glyph, Color accent)
        {
            target = value;
            this.suffix = suffix;
            this.caption = caption;
            this.glyph = glyph;
            this.accent = accent;
            AccessibleName = $"{caption}: {value}{suffix}";
            AccessibleRole = AccessibleRole.StaticText;
            valueFont = Ui.MakeFont(22F, FontStyle.Bold);
            captionFont = Ui.MakeFont(9.5F);
            valueHeight = Ui.LineHeight(valueFont);
            captionHeight = Ui.LineHeight(captionFont);

            // a csempe magassága a betűk magasságából jön, így semmilyen nagyításnál nem vágódik le a szöveg
            int height = Math.Max(Ui.S(96), Ui.S(18) * 2 + valueHeight + captionHeight);
            Size = new Size(Ui.S(232), height);
            Margin = Ui.Pad(0, 0, 16, 16);
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

            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(14)))
            {
                using var fill = new SolidBrush(Theme.CardBg);
                using var border = new Pen(Theme.CardBorder);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            // ikon körben
            int badgeSize = Ui.S(46);
            var badge = new Rectangle(Ui.S(20), (Height - badgeSize) / 2, badgeSize, badgeSize);
            using (var badgeBrush = new SolidBrush(Anim.Lerp(Theme.CardBg, accent, Theme.Dark ? 0.28 : 0.15)))
                g.FillEllipse(badgeBrush, badge);
            var iconFont = Ui.IconFont(16F);
            using (var iconBrush = new SolidBrush(accent))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, iconFont, iconBrush, badge, center);

            int shown = (int)Math.Round(target * progress);
            int textX = badge.Right + Ui.S(16);
            int textWidth = Math.Max(Width - textX - Ui.S(10), 0);

            using var valueBrush = new SolidBrush(Theme.Text);
            using var captionBrush = new SolidBrush(Theme.Muted);
            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };

            // a két sor együtt függőlegesen középre kerül
            int top = (Height - valueHeight - captionHeight) / 2;
            g.DrawString(shown + suffix, valueFont, valueBrush, new Rectangle(textX, top, textWidth, valueHeight + 2), format);
            g.DrawString(caption, captionFont, captionBrush, new Rectangle(textX, top + valueHeight, textWidth, captionHeight + 2), format);
        }
    }

    /// <summary>
    /// Kattintható számlálókártya (ikon, szám, felirat) a Foglalások oldal áttekintőjéhez. A kijelölt kártya
    /// kerete a saját színével jelenik meg.
    /// </summary>
    internal sealed class CountCard : Control
    {
        private readonly string title;
        private readonly string glyph;
        private readonly Color accent;
        private readonly Font valueFont = Ui.MakeFont(20F, FontStyle.Bold);
        private readonly Font titleFont = Ui.MakeFont(9.5F);
        private int count;
        private bool selected;
        private double hoverT;
        private Anim.Tween? hoverTween;

        public CountCard(string title, string glyph, Color accent, Action onClick)
        {
            this.title = title;
            this.glyph = glyph;
            this.accent = accent;

            int height = Math.Max(Ui.S(88), Ui.S(18) * 2 + Ui.LineHeight(valueFont) + Ui.LineHeight(titleFont));
            Size = new Size(Ui.S(232), height);
            Margin = Ui.Pad(0, 0, 16, 16);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, true); // billentyűzettel is megnyitható
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = title;
            Click += (_, _) => onClick();
        }

        public void SetCount(int value)
        {
            count = value;
            AccessibleName = $"{title}: {value}";
            Invalidate();
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


        public void SetSelected(bool value)
        {
            selected = value;
            Invalidate();
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

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(14)))
            {
                using var fill = new SolidBrush(Anim.Lerp(Theme.CardBg, Theme.SoftHover, hoverT));
                using var border = new Pen(selected ? accent : Theme.CardBorder, selected ? 2.5F : 1F);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            int badgeSize = Ui.S(46);
            var badge = new Rectangle(Ui.S(20), (Height - badgeSize) / 2, badgeSize, badgeSize);
            using (var badgeBrush = new SolidBrush(Anim.Lerp(Theme.CardBg, accent, Theme.Dark ? 0.28 : 0.15)))
                g.FillEllipse(badgeBrush, badge);
            using (var iconBrush = new SolidBrush(accent))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, Ui.IconFont(16F), iconBrush, badge, center);

            int textX = badge.Right + Ui.S(16);
            int textWidth = Math.Max(Width - textX - Ui.S(10), 0);
            int valueHeight = Ui.LineHeight(valueFont);
            int top = (Height - valueHeight - Ui.LineHeight(titleFont)) / 2;

            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
            using var valueBrush = new SolidBrush(Theme.Text);
            using var titleBrush = new SolidBrush(selected ? accent : Theme.Muted);
            g.DrawString(count.ToString(), valueFont, valueBrush, new Rectangle(textX, top, textWidth, valueHeight + 2), format);
            g.DrawString(title, titleFont, titleBrush, new Rectangle(textX, top + valueHeight, textWidth, Ui.LineHeight(titleFont) + 2), format);

            if (Focused && ShowFocusCues) Ui.DrawFocusRing(g, new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(14));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) hoverTween?.Cancel();
            base.Dispose(disposing);
        }
    }

    /// <summary>Színátmenetes üdvözlő sáv a főoldal tetején, opcionális gombbal.</summary>
    internal class HeroBanner : Control
    {
        private readonly string title;
        private readonly string subtitle;
        private readonly Font titleFont = Ui.MakeFont(21F, FontStyle.Bold);
        private readonly Font subFont = Ui.MakeFont(10.5F);
        private readonly int padX = Ui.S(28);
        private readonly int titleY;
        private readonly int subtitleY;
        private readonly int titleHeight;
        private readonly int subtitleHeight;

        public HeroBanner(string title, string subtitle, string? buttonText, Action? onButton)
        {
            this.title = title;
            this.subtitle = subtitle;
            Margin = Ui.Pad(0, 0, 0, 20);
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            // a sáv magassága a betűk magasságából számolódik, így semmilyen nagyításnál nem fedi egymást a tartalom
            int padY = Ui.S(24);
            titleHeight = Ui.LineHeight(titleFont);
            subtitleHeight = Ui.LineHeight(subFont);
            titleY = padY;
            subtitleY = titleY + titleHeight + Ui.S(4);
            int bottom = subtitleY + subtitleHeight;

            if (buttonText != null)
            {
                var button = Ui.Btn(buttonText, Theme.White, Theme.MainBlueDark, (_, _) => onButton?.Invoke());
                int buttonY = bottom + Ui.S(16);
                button.Location = new Point(padX, buttonY);
                Controls.Add(button);
                bottom = buttonY + button.GetPreferredSize(Size.Empty).Height;
            }

            Height = bottom + padY;
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

            using var path = Ui.RoundedPath(rect, Ui.S(18));
            using (var brush = new LinearGradientBrush(rect, c1, c2, 20F))
                g.FillPath(brush, path);

            // díszítő körök
            g.SetClip(path);
            using (var circle = new SolidBrush(Color.FromArgb(34, Color.White)))
            {
                g.FillEllipse(circle, Width - Ui.S(190), -Ui.S(60), Ui.S(260), Ui.S(260));
                g.FillEllipse(circle, Width - Ui.S(90), Ui.S(70), Ui.S(150), Ui.S(150));
            }
            g.ResetClip();

            using var white = new SolidBrush(Color.White);
            using var soft = new SolidBrush(Color.FromArgb(225, Color.White));
            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };

            int textWidth = Math.Max(Width - padX * 2, 0);
            g.DrawString(title, titleFont, white, new Rectangle(padX, titleY, textWidth, titleHeight + 2), format);
            g.DrawString(subtitle, subFont, soft, new Rectangle(padX, subtitleY, textWidth, subtitleHeight + 2), format);
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
            Padding = Ui.Pad(22, 18, 22, 18);
            Margin = Ui.Pad(0, 0, 0, 16);
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        /// <summary>A megjelenési animáció állása (0–1): az utódok díszítő elemei is ehhez igazodhatnak.</summary>
        protected double Reveal => reveal;

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
            outline ??= Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(14));
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

    /// <summary>Kártya bal szélén függőleges színes jelzősávval (pl. oktatói kártya).</summary>
    internal sealed class AccentCard : CardPanel
    {
        private readonly Color accent;

        public AccentCard(Color accent)
        {
            this.accent = accent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (Width < 8 || Height < 8) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(14));
            g.SetClip(path);
            using (var brush = new SolidBrush(Color.FromArgb((int)(255 * Reveal), accent)))
                g.FillRectangle(brush, 0, 0, Ui.S(6), Height);
            g.ResetClip();
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
            using var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(18));
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

            Size = Ui.Sz(330, 134);
            Margin = Ui.Pad(0, 0, 18, 18);
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

            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(18)))
            {
                using var fill = new SolidBrush(Theme.CardBg);
                using var border = new Pen(Anim.Lerp(Theme.CardBorder, accent, hoverT), 1F + (float)hoverT);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            // kategória-színű szimbólum-négyzet
            var badge = Ui.Rc(20, 20, 56, 56);
            Color tint = Anim.Lerp(Theme.CardBg, accent, Theme.Dark ? 0.30 : 0.15);
            using (var badgePath = Ui.RoundedPath(badge, Ui.S(16)))
            using (var badgeBrush = new SolidBrush(Anim.Lerp(tint, accent, hoverT)))
                g.FillPath(badgeBrush, badgePath);

            bool symbolFont = glyph[0] < '\uE000'; // a Segoe MDL2 ikonok a privát Unicode-tartományban vannak
            var glyphFont = symbolFont
                ? Ui.SymbolFont(glyph.Length >= 2 ? 13F : 20F, FontStyle.Bold)
                : Ui.IconFont(20F);
            using (var glyphBrush = new SolidBrush(Anim.Lerp(accent, Theme.White, hoverT)))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, glyphFont, glyphBrush, badge, center);

            int textX = badge.Right + Ui.S(16);
            int textWidth = Math.Max(Width - textX - Ui.S(18), 0);
            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };

            var nameFont = Ui.MakeFont(13.5F, FontStyle.Bold);
            using (var nameBrush = new SolidBrush(Theme.Text))
                g.DrawString(name, nameFont, nameBrush, new Rectangle(textX, Ui.S(22), textWidth, Ui.S(28)), format);

            // kategória színes ponttal
            using (var dotBrush = new SolidBrush(accent))
                g.FillEllipse(dotBrush, textX, Ui.S(59), Ui.S(8), Ui.S(8));
            var categoryFont = Ui.MakeFont(9.5F);
            using (var categoryBrush = new SolidBrush(Theme.Muted))
                g.DrawString(category, categoryFont, categoryBrush, new Rectangle(textX + Ui.S(14), Ui.S(52), Math.Max(textWidth - Ui.S(14), 0), Ui.S(22)), format);

            // elválasztó
            using (var line = new Pen(Color.FromArgb(120, Theme.CardBorder)))
                g.DrawLine(line, Ui.S(20), Ui.S(90), Width - Ui.S(20), Ui.S(90));

            // alsó sor: oktatók száma, jobbra "Megnézem →"
            int arrowWidth = clickable ? Ui.S(96) : 0;
            var pillFont = Ui.MakeFont(8.5F, FontStyle.Bold);
            {
                Color pillColor = hasTutors ? Theme.Success : Theme.Muted;
                int pillWidth = Math.Min(TextRenderer.MeasureText(tutorText, pillFont).Width + Ui.S(8), Math.Max(Width - Ui.S(40) - arrowWidth, 0));
                var pill = new Rectangle(Ui.S(20), Ui.S(100), pillWidth, Ui.S(24));
                using var pillPath = Ui.RoundedPath(pill, Ui.S(12));
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
                g.DrawString("Megnézem  →", moreFont, moreBrush, new Rectangle(Width - Ui.S(20) - Ui.S(110) + (int)(Ui.S(6) * hoverT), Ui.S(100), Ui.S(110), Ui.S(24)), moreFormat);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) hoverTween?.Cancel();
            base.Dispose(disposing);
        }
    }
}

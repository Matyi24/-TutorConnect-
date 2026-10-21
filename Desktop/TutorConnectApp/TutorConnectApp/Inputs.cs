using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>
    /// Dupla pufferelt, függőlegesen egymás alá rendező panel. A görgetett tartalom ebből áll: a pufferelés nélkül
    /// minden görgetési lépésnél villogna és szakadozna a kép.
    /// </summary>
    internal sealed class BufferedFlowPanel : FlowLayoutPanel
    {
        public BufferedFlowPanel()
        {
            DoubleBuffered = true;
        }
    }

    /// <summary>
    /// Sima (animált) görgetésű tartalom-panel vékony, modern görgetősávval.
    /// A tartalmat a Content panelbe kell tenni; a szélessége a panelhez igazodik (a jobb oldalon fix sáv
    /// marad a görgetősávnak, így a tartalom nem ugrál). Az egérgörgő a panel fölött mindig működik.
    /// </summary>
    internal class SmoothScrollPanel : Panel, IMessageFilter
    {
        private static int Gutter => Ui.S(14);
        private const int WmMouseWheel = 0x020A;

        /// <summary>A görgetett tartalom: függőlegesen egymás alá rendezett elemek, automatikus magassággal.</summary>
        public readonly FlowLayoutPanel Content;

        private readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
        private double target;
        private double current;
        private long lastTick;
        private bool dragging;
        private bool hoverThumb;
        private int dragOffset;

        public SmoothScrollPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;

            Content = new BufferedFlowPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = Point.Empty,
                Margin = Padding.Empty
            };
            Content.SizeChanged += (_, _) => ClampAndApply();
            Controls.Add(Content);

            timer.Tick += (_, _) => Step();
        }

        private double MaxOffset => Math.Max(0, Content.Height - Height);

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            Content.BackColor = BackColor;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            int width = Math.Max(Width - Gutter, 50);
            Content.MinimumSize = new Size(width, 0);
            Content.MaximumSize = new Size(width, 0);
            ClampAndApply();
        }

        private void ClampAndApply()
        {
            target = Math.Clamp(target, 0, MaxOffset);
            current = Math.Clamp(current, 0, MaxOffset);
            ApplyOffset();
        }

        /// <summary>A görgetési pozíció vagy a tartalom mérete változott (pl. "ugrás az aljára" gomb megjelenítéséhez).</summary>
        public event EventHandler? Scrolled;

        private void ApplyOffset()
        {
            int top = -(int)Math.Round(current);
            if (Content.Top != top) Content.Top = top;
            Invalidate(new Rectangle(Width - Gutter, 0, Gutter, Height));
            Scrolled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Animáltan görget a megadott képpontnyit (pozitív = lefelé).</summary>
        public void ScrollBy(double delta)
        {
            target = Math.Clamp(target + delta, 0, MaxOffset);
            if (!Anim.Enabled)
            {
                current = target;
                ApplyOffset();
                return;
            }

            lastTick = Environment.TickCount64;
            timer.Start();
        }

        /// <summary>Igaz, ha a nézet a tartalom alja közelében van (új elemnél érdemes legörgetni).</summary>
        public bool IsNearEnd() => MaxOffset - current < Ui.S(160);

        /// <summary>A jelenlegi görgetési pozíció (képpont).</summary>
        public double Offset => current;

        /// <summary>Animáció nélkül a megadott pozícióra ugrik (pl. régebbi elemek beszúrása után).</summary>
        public void JumpTo(double offset)
        {
            timer.Stop();
            target = Math.Clamp(offset, 0, MaxOffset);
            current = target;
            ApplyOffset();
        }

        private void Step()
        {
            long now = Environment.TickCount64;
            double seconds = Math.Max(now - lastTick, 1) / 1000.0;
            lastTick = now;

            double diff = target - current;
            if (Math.Abs(diff) < 0.5)
            {
                current = target;
                timer.Stop();
            }
            else
            {
                // időalapú simítás: a képkockasebességtől függetlenül ugyanúgy mozog
                current += diff * (1 - Math.Exp(-seconds * 16));
            }
            ApplyOffset();
        }

        // ------------------------------------------------------------
        // Egérgörgő: az alkalmazás szintjén figyeljük, így fókusz nélkül is működik
        // ------------------------------------------------------------

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Application.AddMessageFilter(this);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Application.RemoveMessageFilter(this);
            base.OnHandleDestroyed(e);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WmMouseWheel || !IsHandleCreated || IsDisposed || !Visible || MaxOffset <= 0)
                return false;

            // csak az aktív ablak (pl. megnyitott dialógus mögötti oldal ne görögjön), és nyitott lista mellett se
            var form = FindForm();
            if (form == null || form != Form.ActiveForm || DropdownSelect.AnyOpen)
                return false;

            if (!RectangleToScreen(ClientRectangle).Contains(Cursor.Position))
                return false;

            int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
            ScrollBy(-delta / 120.0 * Ui.S(110));
            return true;
        }

        // ------------------------------------------------------------
        // Görgetősáv: rajzolás és húzás
        // ------------------------------------------------------------

        private Rectangle ThumbRect()
        {
            double max = MaxOffset;
            if (max <= 0) return Rectangle.Empty;

            int inset = Ui.S(4);
            int track = Height - inset * 2;
            int thumbHeight = Math.Max(Ui.S(40), (int)(track * (double)Height / Content.Height));
            int y = inset + (int)((track - thumbHeight) * (current / max));
            return new Rectangle(Width - Ui.S(10), y, Ui.S(6), thumbHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var thumb = ThumbRect();
            if (thumb.IsEmpty) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Ui.RoundedPath(thumb, Ui.S(3));
            using var brush = new SolidBrush(Color.FromArgb(dragging || hoverThumb ? 180 : 95, Theme.Muted));
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (dragging)
            {
                double free = (Height - Ui.S(8)) - ThumbRect().Height;
                if (free > 0)
                {
                    double ratio = (e.Y - dragOffset - Ui.S(4)) / free;
                    target = current = Math.Clamp(ratio, 0, 1) * MaxOffset;
                    ApplyOffset();
                }
                return;
            }

            var thumb = ThumbRect();
            bool over = !thumb.IsEmpty && new Rectangle(Width - Gutter, thumb.Y, Gutter, thumb.Height).Contains(e.Location);
            if (over != hoverThumb)
            {
                hoverThumb = over;
                Invalidate(new Rectangle(Width - Gutter, 0, Gutter, Height));
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || e.X < Width - Gutter) return;

            var thumb = ThumbRect();
            if (thumb.IsEmpty) return;

            if (e.Y >= thumb.Top && e.Y <= thumb.Bottom)
            {
                dragging = true;
                dragOffset = e.Y - thumb.Top;
                Capture = true;
            }
            else
            {
                ScrollBy(e.Y < thumb.Top ? -Height * 0.9 : Height * 0.9); // a sávra kattintva egy oldalnyit ugrik
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Capture = false;
            Invalidate(new Rectangle(Width - Gutter, 0, Gutter, Height));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverThumb)
            {
                hoverThumb = false;
                Invalidate(new Rectangle(Width - Gutter, 0, Gutter, Height));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Application.RemoveMessageFilter(this);
                timer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Appos keresőmező: lekerekített "pirula" nagyító-ikonnal, törlés gombbal és animált fókusz-kerettel.
    /// </summary>
    internal class SearchBox : Control
    {
        private readonly TextBox input;
        private double focusT;
        private Anim.Tween? focusTween;
        private bool hoverClear;

        public event EventHandler? ValueChanged;

        public SearchBox(string placeholder)
        {
            Height = Ui.S(50);
            Margin = Ui.Pad(0, 0, 0, 16);
            Cursor = Cursors.IBeam;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            input = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Ui.MakeFont(11.5F),
                PlaceholderText = placeholder,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                AccessibleName = placeholder
            };
            input.GotFocus += (_, _) => FocusTo(1);
            input.LostFocus += (_, _) => FocusTo(0);
            input.TextChanged += (_, _) =>
            {
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };
            Controls.Add(input);
            LayoutInput();
        }

        public string Value => input.Text;

        public void SetValue(string text) => input.Text = text;

        /// <summary>A kurzor a mezőbe kerül (Ctrl+F), a meglévő keresőszöveg kijelölve.</summary>
        public void FocusInput()
        {
            input.Focus();
            input.SelectAll();
        }

        private Rectangle ClearRect => new(Width - Ui.S(44), (Height - Ui.S(30)) / 2, Ui.S(30), Ui.S(30));

        private void LayoutInput()
        {
            // a konstruktor közben már érkezhet méretváltozás, amikor az input még nem létezik
            if (input is null) return;

            input.Left = Ui.S(52);
            input.Width = Math.Max(Width - Ui.S(52) - Ui.S(50), 20);
            input.Top = (Height - input.Height) / 2;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutInput();
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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = input.TextLength > 0 && ClearRect.Contains(e.Location);
            if (over != hoverClear)
            {
                hoverClear = over;
                Cursor = over ? Cursors.Hand : Cursors.IBeam;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverClear)
            {
                hoverClear = false;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (input.TextLength > 0 && ClearRect.Contains(e.Location))
                input.Clear();
            input.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2))
            {
                using var fill = new SolidBrush(Theme.InputBg);
                using var border = new Pen(Anim.Lerp(Theme.CardBorder, Theme.MainBlue, focusT), 1F + (float)focusT);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            var iconFont = Ui.IconFont(13F);
            using (var iconBrush = new SolidBrush(Anim.Lerp(Theme.Muted, Theme.MainBlue, focusT)))
                g.DrawString("\uE721", iconFont, iconBrush, new Rectangle(Ui.S(14), 0, Ui.S(34), Height), format);

            if (input.TextLength > 0)
            {
                if (hoverClear)
                {
                    using var hoverBrush = new SolidBrush(Theme.SoftHover);
                    g.FillEllipse(hoverBrush, ClearRect);
                }
                var clearFont = Ui.IconFont(9F);
                using var clearBrush = new SolidBrush(hoverClear ? Theme.Text : Theme.Muted);
                g.DrawString("\uE711", clearFont, clearBrush, ClearRect, format);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) focusTween?.Cancel();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Appos legördülő választó: címke + az aktuális érték, kattintásra lekerekített lista nyílik.
    /// </summary>
    internal class DropdownSelect : Control
    {
        /// <summary>Igaz, ha valamelyik lista éppen nyitva van (ilyenkor az oldal nem görgethető az egérgörgővel).</summary>
        public static bool AnyOpen;

        private readonly string caption;
        private List<string> items;
        private int selectedIndex;
        private double hoverT;
        private Anim.Tween? hoverTween;
        private ToolStripDropDown? popup;
        private long lastClosed;

        public event EventHandler? SelectionChanged;

        public DropdownSelect(string caption, IEnumerable<string> options, int selected = 0)
        {
            this.caption = caption;
            items = options.ToList();
            selectedIndex = Math.Clamp(selected, 0, Math.Max(items.Count - 1, 0));

            Size = Ui.Sz(250, 58);
            Margin = Ui.Pad(0, 0, 14, 14);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, true); // Enter / szóköz nyitja a listát
            TabStop = true;
            AccessibleRole = AccessibleRole.ComboBox;
            UpdateAccessibleName();
        }

        private void UpdateAccessibleName() =>
            AccessibleName = items.Count == 0 ? caption : $"{caption}: {items[Math.Clamp(selectedIndex, 0, items.Count - 1)]}";

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space && !IsOpen)
            {
                e.Handled = true;
                Open();
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        public int SelectedIndex => selectedIndex;

        public string SelectedText => items.Count == 0 ? "" : items[selectedIndex];

        /// <summary>A kijelölés beállítása kódból (esemény nélkül).</summary>
        public void SetSelected(int index)
        {
            selectedIndex = Math.Clamp(index, 0, Math.Max(items.Count - 1, 0));
            UpdateAccessibleName();
            Invalidate();
        }

        private bool IsOpen => popup is { Visible: true };

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

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            // a nyitott listát a mező egy kattintása zárja be (a lista maga az egér lenyomására záródik)
            if (!IsOpen && Environment.TickCount64 - lastClosed > 250)
                Open();
        }

        private void Open()
        {
            if (items.Count == 0) return;
            ClosePopup();

            var list = new DropdownList(items, selectedIndex, Width);
            list.ItemChosen += index =>
            {
                selectedIndex = index;
                UpdateAccessibleName();
                popup?.Close();
                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            };

            var host = new ToolStripControlHost(list)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = list.Size
            };

            popup = new ToolStripDropDown
            {
                Padding = Padding.Empty,
                AutoClose = true,
                DropShadowEnabled = true,
                BackColor = Theme.CardBg
            };
            popup.Items.Add(host);
            popup.Closed += (_, _) =>
            {
                AnyOpen = false;
                lastClosed = Environment.TickCount64;
                Invalidate();
            };

            AnyOpen = true;
            popup.Show(this, new Point(0, Height + Ui.S(4)));
            Invalidate();
        }

        private void ClosePopup()
        {
            AnyOpen = false;
            popup?.Dispose();
            popup = null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            double highlight = IsOpen ? 1 : hoverT;
            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(16)))
            {
                using var fill = new SolidBrush(Theme.InputBg);
                using var border = new Pen(Anim.Lerp(Theme.CardBorder, Theme.MainBlue, highlight), 1F + (float)highlight);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
            int textWidth = Math.Max(Width - Ui.S(18) - Ui.S(46), 0);

            var captionFont = Ui.MakeFont(8.5F);
            using (var captionBrush = new SolidBrush(Theme.Muted))
                g.DrawString(caption, captionFont, captionBrush, new Rectangle(Ui.S(18), Ui.S(9), textWidth, Ui.S(16)), format);

            var valueFont = Ui.MakeFont(10.5F, FontStyle.Bold);
            using (var valueBrush = new SolidBrush(Theme.Text))
                g.DrawString(SelectedText, valueFont, valueBrush, new Rectangle(Ui.S(18), Ui.S(27), textWidth, Ui.S(24)), format);

            var chevronFont = Ui.IconFont(9F);
            using var chevronBrush = new SolidBrush(Anim.Lerp(Theme.Muted, Theme.MainBlue, highlight));
            using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(IsOpen ? "\uE70E" : "\uE70D", chevronFont, chevronBrush, new Rectangle(Width - Ui.S(40), 0, Ui.S(30), Height), center);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                hoverTween?.Cancel();
                ClosePopup();
            }
            base.Dispose(disposing);
        }

        /// <summary>A legördülő lista: lekerekített sorok, kiemelés, kijelölt elem pipával; sok elemnél görgethető.</summary>
        private sealed class DropdownList : Control
        {
            private const int MaxRows = 8;
            private static int RowHeight => Ui.S(40);
            private static int Pad => Ui.S(6);

            private readonly List<string> items;
            private readonly int selected;
            private int hover = -1;
            private int offset;

            public event Action<int>? ItemChosen;

            public DropdownList(List<string> items, int selected, int width)
            {
                this.items = items;
                this.selected = selected;
                Size = new Size(width, Math.Min(items.Count, MaxRows) * RowHeight + Pad * 2);
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

                // hosszú listánál a kijelölt elem látszódjon
                offset = Math.Clamp((selected - MaxRows / 2) * RowHeight, 0, MaxOffset);
            }

            private int MaxOffset => Math.Max(0, items.Count * RowHeight - (Height - Pad * 2));

            private int IndexAt(int y)
            {
                if (y < Pad || y > Height - Pad) return -1;
                int index = (y - Pad + offset) / RowHeight;
                return index >= 0 && index < items.Count ? index : -1;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int index = IndexAt(e.Y);
                if (index != hover)
                {
                    hover = index;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                hover = -1;
                Invalidate();
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                offset = Math.Clamp(offset - Math.Sign(e.Delta) * RowHeight, 0, MaxOffset);
                hover = IndexAt(e.Y);
                Invalidate();
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                int index = IndexAt(e.Y);
                if (index >= 0) ItemChosen?.Invoke(index);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Theme.CardBg);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                g.SetClip(new Rectangle(0, Pad, Width, Height - Pad * 2));
                using var format = new StringFormat(StringFormatFlags.NoWrap)
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                var checkFont = Ui.IconFont(10F);

                for (int i = 0; i < items.Count; i++)
                {
                    int y = Pad + i * RowHeight - offset;
                    if (y + RowHeight < 0 || y > Height) continue;

                    var row = new Rectangle(Ui.S(6), y + Ui.S(2), Width - Ui.S(13), RowHeight - Ui.S(4));
                    bool isSelected = i == selected;

                    if (i == hover || isSelected)
                    {
                        using var path = Ui.RoundedPath(row, Ui.S(10));
                        using var rowBrush = new SolidBrush(i == hover ? Theme.ChipBg : Theme.SoftHover);
                        g.FillPath(rowBrush, path);
                    }

                    var font = Ui.MakeFont(10.5F, isSelected ? FontStyle.Bold : FontStyle.Regular);
                    using var textBrush = new SolidBrush(isSelected ? Theme.MainBlue : Theme.Text);
                    g.DrawString(items[i], font, textBrush, new Rectangle(row.X + Ui.S(12), row.Y, row.Width - Ui.S(40), row.Height), format);

                    if (isSelected)
                    {
                        using var checkBrush = new SolidBrush(Theme.MainBlue);
                        g.DrawString("\uE73E", checkFont, checkBrush, new Rectangle(row.Right - Ui.S(30), row.Y, Ui.S(24), row.Height), format);
                    }
                }
                g.ResetClip();

                using var borderPen = new Pen(Theme.CardBorder);
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}

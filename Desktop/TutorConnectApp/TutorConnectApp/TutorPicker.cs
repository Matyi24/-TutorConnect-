using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>
    /// Oktató-kereső mező: gépelés közben megjelenik, kire gondolhattál (név és tantárgy alapján, ékezetek nélkül is), és
    /// egy kattintással (vagy nyilakkal és Enterrel) kiválasztható. Üres mezőnél a kedvencek ajánlódnak fel.
    /// A javaslatlista a mező alatt, az oldal fölött lebeg, így nem tolja el a tartalmat.
    /// </summary>
    internal sealed class TutorPicker : Control
    {
        private readonly List<TutorDto> tutors;
        private readonly TextBox input;
        private TutorDto? selected;
        private SuggestionList? list;
        private bool suppress;
        private bool enabled = true;
        private double focusT;
        private double hoverT;
        private Anim.Tween? focusTween;
        private Anim.Tween? hoverTween;
        private bool hoverClear;

        /// <summary>A kiválasztott oktató (null: kiürítették a mezőt).</summary>
        public event Action<TutorDto?>? TutorChosen;

        public TutorPicker(List<TutorDto> tutors, TutorDto? initial)
        {
            this.tutors = tutors;
            selected = initial;

            Size = Ui.Sz(380, 58);
            Margin = Ui.Pad(0, 0, 14, 14);
            Cursor = Cursors.IBeam;
            AccessibleRole = AccessibleRole.ComboBox;
            AccessibleName = "Oktató keresése";
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            input = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Ui.MakeFont(10.5F, FontStyle.Bold),
                PlaceholderText = "Kezdd el írni az oktató nevét vagy tantárgyát...",
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                MaxLength = 60,
                AccessibleName = "Oktató neve (a javaslatok közül választhatsz)"
            };
            input.GotFocus += (_, _) =>
            {
                FocusTo(1);
                if (input.TextLength == 0) ShowSuggestions(); // üresen a kedvencek
            };
            input.LostFocus += (_, _) =>
            {
                FocusTo(0);
                CloseList();
            };
            input.TextChanged += (_, _) =>
            {
                if (suppress) return;

                Invalidate();
                if (input.TextLength == 0 && selected != null)
                {
                    selected = null;
                    TutorChosen?.Invoke(null);
                }
                ShowSuggestions();
            };
            input.KeyDown += OnInputKeyDown;
            input.MouseEnter += (_, _) => HoverTo(1);
            input.MouseLeave += (_, _) => HoverTo(0);
            Controls.Add(input);

            if (initial != null) SetInput(initial.FullName);
            LayoutInput();
        }

        public TutorDto? Selected => selected;

        /// <summary>Kedvencek módban a mező halvány és nem használható.</summary>
        public void SetEnabledLook(bool value)
        {
            enabled = value;
            input.Enabled = value;
            if (!value) CloseList();
            Invalidate();
        }

        private void SetInput(string text)
        {
            suppress = true;
            input.Text = text;
            suppress = false;
            Invalidate();
        }

        private int AvatarSpace => selected != null ? Ui.S(46) : 0;

        private Rectangle ClearRect => new(Width - Ui.S(44), (Height - Ui.S(30)) / 2, Ui.S(30), Ui.S(30));

        private void LayoutInput()
        {
            if (input is null) return;

            input.Left = Ui.S(18) + AvatarSpace;
            input.Width = Math.Max(Width - input.Left - Ui.S(52), 20);
            input.Top = Ui.S(27);
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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = input.TextLength > 0 && ClearRect.Contains(e.Location);
            if (over == hoverClear) return;

            hoverClear = over;
            Cursor = over ? Cursors.Hand : Cursors.IBeam;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!enabled) return;

            if (input.TextLength > 0 && ClearRect.Contains(e.Location))
            {
                Clear();
                return;
            }
            input.Focus();
        }

        /// <summary>A mező kiürítése és a kiválasztás megszüntetése (✕).</summary>
        public void Clear()
        {
            bool had = selected != null;
            selected = null;
            SetInput("");
            LayoutInput();
            CloseList();
            input.Focus();
            if (had) TutorChosen?.Invoke(null);
        }

        // ------------------------------------------------------------
        // Javaslatok
        // ------------------------------------------------------------

        private (List<TutorDto> Items, string Header, int More) Compute()
        {
            string query = input.Text.Trim();
            if (query.Length == 0)
            {
                var favorites = tutors.Where(t => Favorites.IsFavorite(t.Id)).Take(6).ToList();
                return (favorites, "Kedvenc oktatóid", 0);
            }

            var items = TutorSearch.Suggest(tutors, query, 6);
            int total = TutorSearch.CountMatches(tutors, query);
            return (items, items.Count > 0 ? "Erre gondoltál?" : "", Math.Max(total - items.Count, 0));
        }

        private Control? Host => FindPage() ?? (Control?)FindForm();

        private Control? FindPage()
        {
            for (Control? c = Parent; c != null; c = c.Parent)
                if (c is PageBase) return c;
            return null;
        }

        private void ShowSuggestions()
        {
            if (!enabled || !input.Focused) return;

            var (items, header, more) = Compute();
            var host = Host;
            if (host == null) return;

            if (items.Count == 0)
            {
                // nincs egyezés: a lista helyett rövid üzenet, ha írt valamit
                if (input.TextLength == 0) { CloseList(); return; }
                ShowList(host, new List<TutorDto>(), "Nincs ilyen oktató. Próbálj más nevet vagy tantárgyat.", 0);
                return;
            }

            ShowList(host, items, header, more);
        }

        private void ShowList(Control host, List<TutorDto> items, string header, int more)
        {
            if (list == null)
            {
                list = new SuggestionList();
                list.Chosen += Choose;
                host.Controls.Add(list);
                DropdownSelect.AnyOpen = true; // amíg a lista nyitva van, az oldal nem görgethető az egérgörgővel
            }

            list.SetItems(items, header, more, Width);
            var origin = host.PointToClient(PointToScreen(new Point(0, Height + Ui.S(4))));
            list.Location = origin;
            list.BringToFront();
        }

        private void CloseList()
        {
            if (list == null) return;

            list.Parent?.Controls.Remove(list);
            list.Dispose();
            list = null;
            DropdownSelect.AnyOpen = false;
        }

        private void Choose(TutorDto tutor)
        {
            selected = tutor;
            SetInput(tutor.FullName);
            LayoutInput();
            CloseList();
            TutorChosen?.Invoke(tutor);
        }

        private void OnInputKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Down:
                    if (list == null) ShowSuggestions();
                    list?.MoveSelection(1);
                    e.Handled = e.SuppressKeyPress = true;
                    break;
                case Keys.Up:
                    list?.MoveSelection(-1);
                    e.Handled = e.SuppressKeyPress = true;
                    break;
                case Keys.Enter:
                    if (list?.Current is { } highlighted) Choose(highlighted);
                    else if (list?.First is { } first) Choose(first);
                    e.Handled = e.SuppressKeyPress = true;
                    break;
                case Keys.Escape:
                    if (list != null)
                    {
                        CloseList();
                        e.Handled = e.SuppressKeyPress = true;
                    }
                    break;
            }
        }

        // ------------------------------------------------------------
        // Rajzolás
        // ------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            double highlight = Math.Max(focusT, hoverT * 0.7);
            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(16)))
            using (var fill = new SolidBrush(enabled ? Theme.InputBg : Theme.SoftHover))
            using (var border = new Pen(Anim.Lerp(Theme.CardBorder, Theme.MainBlue, highlight), 1F + (float)focusT))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            TextRenderer.DrawText(g, "Oktató", Ui.MakeFont(8.5F),
                new Rectangle(Ui.S(18) + AvatarSpace, Ui.S(8), Math.Max(Width - Ui.S(36), 0), Ui.S(17)), Theme.Muted,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            if (selected != null)
            {
                int d = Ui.S(32);
                Ui.DrawAvatar(g, new Rectangle(Ui.S(14), (Height - d) / 2, d, d), Ui.Initials(selected.FullName), Theme.MainBlue, Theme.White);
            }

            if (input.TextLength > 0)
            {
                using var brush = new SolidBrush(hoverClear ? Theme.Danger : Theme.Muted);
                using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("", Ui.IconFont(10F), brush, ClearRect, center);
            }
            else
            {
                using var brush = new SolidBrush(Theme.Muted);
                using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("", Ui.IconFont(11F), brush, ClearRect, center);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseList();
                focusTween?.Cancel();
                hoverTween?.Cancel();
            }
            base.Dispose(disposing);
        }

        /// <summary>A lebegő javaslatlista: fejléc, sorok (avatar, név, tantárgyak, értékelés), "további találat" jelzés.</summary>
        private sealed class SuggestionList : Control
        {
            private List<TutorDto> items = new();
            private string header = "";
            private int more;
            private int hover = -1;
            private int highlight = -1;

            public event Action<TutorDto>? Chosen;

            private static int RowHeight => Ui.S(58);
            private static int HeaderHeight => Ui.S(34);
            private static int FooterHeight => Ui.S(32);

            public SuggestionList()
            {
                Cursor = Cursors.Hand;
                AccessibleRole = AccessibleRole.List;
                AccessibleName = "Oktató-javaslatok";
                SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.Selectable, false); // nem veszi el a fókuszt a szövegmezőtől
            }

            public TutorDto? Current => highlight >= 0 && highlight < items.Count ? items[highlight] : null;

            public TutorDto? First => items.Count > 0 ? items[0] : null;

            public void SetItems(List<TutorDto> newItems, string newHeader, int newMore, int width)
            {
                items = newItems;
                header = newHeader;
                more = newMore;
                hover = -1;
                highlight = -1;

                int rows = Math.Max(items.Count, 1);
                Size = new Size(Math.Max(width, Ui.S(300)),
                    (header.Length > 0 ? HeaderHeight : Ui.S(8)) + (items.Count == 0 ? Ui.S(46) : rows * RowHeight) + (more > 0 ? FooterHeight : Ui.S(8)));
                Invalidate();
            }

            public void MoveSelection(int delta)
            {
                if (items.Count == 0) return;
                highlight = highlight < 0 ? (delta > 0 ? 0 : items.Count - 1) : (highlight + delta + items.Count) % items.Count;
                Invalidate();
            }

            private int IndexAt(int y)
            {
                int top = header.Length > 0 ? HeaderHeight : Ui.S(8);
                if (y < top) return -1;
                int index = (y - top) / RowHeight;
                return index >= 0 && index < items.Count ? index : -1;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int index = IndexAt(e.Y);
                if (index == hover) return;
                hover = index;
                highlight = index;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                hover = -1;
                Invalidate();
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                int index = IndexAt(e.Y);
                if (index >= 0) Chosen?.Invoke(items[index]);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Ui.EffectiveBack(Parent));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                // enyhe árnyék, majd a lekerekített lap
                var card = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = Ui.RoundedPath(card, Ui.S(14)))
                {
                    using var fill = new SolidBrush(Theme.CardBg);
                    using var border = new Pen(Anim.Lerp(Theme.CardBorder, Theme.MainBlue, 0.4), 1.2F);
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }

                int y = Ui.S(8);
                if (header.Length > 0)
                {
                    bool empty = items.Count == 0;
                    TextRenderer.DrawText(g, header, Ui.MakeFont(9F, FontStyle.Bold),
                        new Rectangle(Ui.S(16), Ui.S(6), Width - Ui.S(32), Ui.S(22)), empty ? Theme.Muted : Theme.MainBlue,
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
                    y = HeaderHeight;
                }

                for (int i = 0; i < items.Count; i++)
                {
                    var tutor = items[i];
                    var row = new Rectangle(Ui.S(6), y, Width - Ui.S(12), RowHeight - Ui.S(2));

                    if (i == highlight)
                    {
                        using var path = Ui.RoundedPath(row, Ui.S(10));
                        using var fill = new SolidBrush(Theme.ChipBg);
                        g.FillPath(fill, path);
                    }

                    int d = Ui.S(38);
                    Ui.DrawAvatar(g, new Rectangle(row.X + Ui.S(8), row.Y + (row.Height - d) / 2, d, d), Ui.Initials(tutor.FullName), Theme.MainBlue, Theme.White);

                    string rating = tutor.ReviewCount > 0 ? $"★ {tutor.AverageRating.ToString("0.0", Ui.Hu)}" : "";
                    int ratingWidth = rating.Length == 0 ? 0 : TextRenderer.MeasureText(rating, Ui.MakeFont(9.5F, FontStyle.Bold)).Width + Ui.S(6);
                    int textX = row.X + Ui.S(8) + d + Ui.S(12);
                    int right = row.Right - Ui.S(10);
                    var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

                    TextRenderer.DrawText(g, tutor.FullName, Ui.MakeFont(10.5F, FontStyle.Bold),
                        new Rectangle(textX, row.Y + Ui.S(8), Math.Max(right - ratingWidth - textX, 0), Ui.S(22)), Theme.Text, flags);
                    if (rating.Length > 0)
                        TextRenderer.DrawText(g, rating, Ui.MakeFont(9.5F, FontStyle.Bold),
                            new Rectangle(right - ratingWidth, row.Y + Ui.S(9), ratingWidth, Ui.S(20)), Theme.Star, flags | TextFormatFlags.Right);
                    TextRenderer.DrawText(g, string.IsNullOrWhiteSpace(tutor.Subjects) ? "Nincs megadott tantárgy" : tutor.Subjects!,
                        Ui.MakeFont(9F), new Rectangle(textX, row.Y + Ui.S(30), Math.Max(right - textX, 0), Ui.S(20)), Theme.Muted, flags);

                    y += RowHeight;
                }

                if (items.Count == 0)
                {
                    // csak az üzenet látszik (a fejléc tartalmazza)
                }

                if (more > 0)
                {
                    TextRenderer.DrawText(g, $"+ {more} további találat: írj még pár betűt a szűkítéshez", Ui.MakeFont(9F),
                        new Rectangle(Ui.S(16), Height - FooterHeight, Width - Ui.S(32), FooterHeight - Ui.S(4)), Theme.Muted,
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }
    }
}

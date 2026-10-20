using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>Egy találat a gyorskeresőben (Ctrl+K): oldal, művelet, beszélgetés vagy oktató.</summary>
    internal sealed record QuickEntry(string Kind, string Key, int Id, string Title, string Subtitle, string Glyph);

    /// <summary>Egy sor a gyorskereső listájában.</summary>
    internal sealed class QuickItem : Control
    {
        public readonly QuickEntry Entry;
        private bool selected;

        public QuickItem(QuickEntry entry, int width)
        {
            Entry = entry;
            Size = new Size(width, Ui.S(56));
            Margin = Ui.Pad(0, 0, 0, 4);
            Cursor = Cursors.Hand;
            AccessibleName = $"{entry.Title}, {entry.Subtitle}";
            AccessibleRole = AccessibleRole.ListItem;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        public void SetSelected(bool value)
        {
            selected = value;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(12)))
            {
                using var fill = new SolidBrush(selected ? Theme.ChipBg : Theme.CardBg);
                using var border = new Pen(selected ? Theme.MainBlue : Theme.CardBorder, selected ? 2F : 1F);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            int badge = Ui.S(34);
            var circle = new Rectangle(Ui.S(12), (Height - badge) / 2, badge, badge);
            using (var fill = new SolidBrush(Anim.Lerp(Theme.CardBg, Theme.MainBlue, Theme.Dark ? 0.35 : 0.16)))
                g.FillEllipse(fill, circle);

            bool symbol = Entry.Glyph.Length > 0 && Entry.Glyph[0] < '';
            using (var iconBrush = new SolidBrush(Theme.MainBlue))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(Entry.Glyph, symbol ? Ui.MakeFont(11F, FontStyle.Bold) : Ui.IconFont(12F), iconBrush, circle, center);

            int textX = circle.Right + Ui.S(12);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, Entry.Title, Ui.MakeFont(10.5F, FontStyle.Bold),
                new Rectangle(textX, Ui.S(8), Width - textX - Ui.S(12), Ui.S(22)), Theme.Text, flags);
            TextRenderer.DrawText(g, Entry.Subtitle, Ui.MakeFont(9F),
                new Rectangle(textX, Ui.S(30), Width - textX - Ui.S(12), Ui.S(20)), Theme.Muted, flags);
        }
    }

    /// <summary>
    /// Gyorskereső (Ctrl+K): gépelésre szűri az oldalakat, a beszélgetéseket és (diáknál) az oktatókat.
    /// Fel / le nyíllal lehet választani, Enter nyitja meg.
    /// </summary>
    internal sealed class QuickSwitcherDialog : DialogBase
    {
        private const int MaxShown = 8;

        private readonly List<QuickEntry> all = new();
        private readonly TextBox searchBox;
        private readonly FlowLayoutPanel list;
        private readonly List<QuickItem> items = new();
        private int selectedIndex;

        public QuickEntry? Chosen { get; private set; }

        public QuickSwitcherDialog(IEnumerable<QuickEntry> baseEntries)
            : base("Gyorskereső", "Ugorj bárhova: oldalra, beszélgetésre vagy oktatóra.", "Megnyitás", Ui.Sz(560, 640), "")
        {
            all.AddRange(baseEntries);
            all.AddRange(ChatState.Conversations.Select(c =>
                new QuickEntry("chat", "chat", c.Id, c.OtherUserName, "Beszélgetés", "")));

            Section("Mit keresel?");
            searchBox = NewInput("", 60);
            searchBox.PlaceholderText = "Kezdj el gépelni...";
            searchBox.TextChanged += (_, _) =>
            {
                selectedIndex = 0;
                Render();
            };
            searchBox.KeyDown += OnSearchKeyDown;
            AddBody(searchBox);

            Section("Találatok");
            list = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            AddBody(list);

            SetFooter("Fel / le nyíl: választás · Enter: megnyitás · Esc: mégse");
            Render();
        }

        /// <summary>Az oktatók (diáknak) később, a lista betöltése után kerülnek be.</summary>
        public void AddEntries(IEnumerable<QuickEntry> more)
        {
            all.AddRange(more);
            Render();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            searchBox.Focus();
        }

        private void Render()
        {
            string query = searchBox.Text.Trim();
            var matches = all
                .Where(entry => query.Length == 0 ? entry.Kind is "page" or "action" : Ui.ContainsText(entry.Title, query) || Ui.ContainsText(entry.Subtitle, query))
                .Take(MaxShown)
                .ToList();

            selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(matches.Count - 1, 0));

            list.SuspendLayout();
            Ui.Reset(list);
            items.Clear();

            if (matches.Count == 0)
            {
                list.Controls.Add(Ui.Lbl("Nincs találat.", 10.5F, Theme.Muted, margin: Ui.Pad(0, 4, 0, 4)));
            }
            else
            {
                for (int i = 0; i < matches.Count; i++)
                {
                    var item = new QuickItem(matches[i], ContentWidth);
                    int index = i;
                    item.Click += (_, _) => Choose(index);
                    list.Controls.Add(item);
                    items.Add(item);
                }
            }
            list.ResumeLayout(true);
            Highlight();
        }

        private void Highlight()
        {
            for (int i = 0; i < items.Count; i++)
                items[i].SetSelected(i == selectedIndex);

            ActionButton.Enabled = items.Count > 0;
            SetFooter(items.Count > 0 ? items[selectedIndex].Entry.Title + "\n" + items[selectedIndex].Entry.Subtitle : "Fel / le nyíl: választás · Enter: megnyitás · Esc: mégse");
        }

        private void OnSearchKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Down when items.Count > 0:
                    selectedIndex = (selectedIndex + 1) % items.Count;
                    Highlight();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Up when items.Count > 0:
                    selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
                    Highlight();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Enter:
                    e.SuppressKeyPress = true;
                    if (items.Count > 0) Choose(selectedIndex);
                    break;
            }
        }

        private void Choose(int index)
        {
            if (index < 0 || index >= items.Count) return;
            Chosen = items[index].Entry;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override Task OnActionAsync()
        {
            Choose(selectedIndex);
            return Task.CompletedTask;
        }
    }
}

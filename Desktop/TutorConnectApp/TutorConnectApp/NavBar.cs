using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>
    /// Bal oldali navigációs sáv. Menüpont (kijelölhető oldal): AddItem("kulcs", "Felirat", "\uE80F").
    /// Művelet-gomb (nem kerül kijelölésre, pl. kijelentkezés): AddAction(...).
    /// Az ikon a Segoe MDL2 Assets betűkészlet egy karaktere (opcionális).
    /// Keskeny ablaknál animáltan összecsukódik, és csak az ikonok látszanak.
    /// </summary>
    public class NavBar : UserControl
    {
        private readonly BrandHeader header;
        private readonly UserChip userChip;
        private readonly FlowLayoutPanel itemsPanel;
        private readonly Dictionary<string, NavButton> buttons = new();
        private readonly ToolTip tips = new();

        private bool collapsed;
        private bool initialized;

        /// <summary>Menüpontra vagy műveletre kattintáskor fut le, a paraméter a kulcsa.</summary>
        public event EventHandler<string>? ItemClicked;

        public NavBar()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Left;
            Width = Ui.S(290); // az ApplyResponsive() később az ablakhoz igazítja
            BackColor = Theme.NavBg;

            itemsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true, // ha az ablak annyira alacsony, hogy a gombok akkor sem férnek el, görgethető marad (semmi sem vész el)
                Padding = Ui.Pad(14, 14, 14, 0),
                BackColor = Theme.NavBg
            };
            itemsPanel.Resize += (_, _) => ResizeButtons();

            header = new BrandHeader { Dock = DockStyle.Top, Height = Ui.S(108) };

            userChip = new UserChip { Dock = DockStyle.Bottom, Height = Ui.S(96) };
            userChip.Click += (_, _) => Select("profile"); // a felhasználóra kattintva a profil nyílik meg

            Controls.Add(itemsPanel); // a kitöltő (Fill) elem legyen elöl a sorrendben
            Controls.Add(header);
            Controls.Add(userChip);
        }

        /// <summary>A navbár aljára kerülő felhasználói blokk (avatar, név, szerepkör).</summary>
        public void SetUser(string name, string role)
        {
            userChip.SetUser(name, Ui.RoleName(role));
        }

        public void AddItem(string key, string text, string icon = "")
        {
            var button = AddButton(key, text, icon);
            button.Click += (_, _) => Select(key);
        }

        public void AddAction(string key, string text, string icon = "")
        {
            var button = AddButton(key, text, icon);
            button.Click += (_, _) => ItemClicked?.Invoke(this, key);
        }

        private NavButton AddButton(string key, string text, string icon)
        {
            var button = new NavButton(text, icon) { Margin = Ui.Pad(0, 0, 0, 8) };
            button.SetCollapsed(collapsed);
            buttons[key] = button;
            itemsPanel.Controls.Add(button);
            ResizeButtons();
            return button;
        }

        /// <summary>Jelvény (olvasatlan üzenetek, függő foglalások száma) egy menüponton; 0 = nincs jelvény.</summary>
        public void SetBadge(string key, int count)
        {
            if (buttons.TryGetValue(key, out var button))
                button.SetBadge(count);
        }

        /// <summary>Egy gomb feliratának és ikonjának cseréje (pl. a téma-váltó gombnál).</summary>
        public void SetItemContent(string key, string text, string icon)
        {
            if (buttons.TryGetValue(key, out var button))
            {
                button.SetContent(text, icon);
                tips.SetToolTip(button, collapsed ? text : "");
            }
        }

        public void Select(string key)
        {
            if (!buttons.ContainsKey(key)) return;

            foreach (var pair in buttons)
                pair.Value.SetActive(pair.Key == key);

            ItemClicked?.Invoke(this, key);
        }

        /// <summary>A téma váltásakor hívandó: újrafesti a sávot az új színekkel.</summary>
        public void ApplyTheme()
        {
            BackColor = Theme.NavBg;
            itemsPanel.BackColor = Theme.NavBg;
            header.Invalidate();
            userChip.Invalidate();
            foreach (var button in buttons.Values)
                button.Invalidate();
            Invalidate();
        }

        /// <summary>
        /// Reszponzív méretezés: az ablak szélességétől függően változik a sáv szélessége,
        /// keskeny ablaknál összecsukódik (csak ikonok látszanak).
        /// </summary>
        public void ApplyResponsive(int availableWidth)
        {
            bool wantCollapsed = availableWidth < 800;
            if (!initialized || wantCollapsed != collapsed)
            {
                initialized = true;
                collapsed = wantCollapsed;
                SetCollapsedMode(wantCollapsed);
            }

            Width = collapsed ? Ui.S(92) : Math.Clamp((int)(availableWidth * 0.22), Ui.S(270), Ui.S(340));
        }

        /// <summary>Másik DPI-jű monitorra húzáskor: a rögzített méretek újraszámolása.</summary>
        public void ApplyScale()
        {
            header.Height = Ui.S(108);
            userChip.Height = Ui.S(96);
            itemsPanel.Padding = Ui.Pad(14, 14, 14, 0);
            foreach (var button in buttons.Values)
                button.Margin = Ui.Pad(0, 0, 0, 8);
            ResizeButtons();
            header.Invalidate();
            userChip.Invalidate();
        }

        private void SetCollapsedMode(bool value)
        {
            header.SetCollapsed(value);
            userChip.SetCollapsed(value);
            foreach (var button in buttons.Values)
            {
                button.SetCollapsed(value);
                tips.SetToolTip(button, value ? button.Text : "");
            }
            ResizeButtons();
        }

        /// <summary>
        /// A gombok szélessége a sávhoz igazodik, a magasságuk az elérhető helyhez: sok menüpontnál (pl. diáknál tíz) alacsonyabbak lesznek,
        /// hogy mind látsszon, a Beállítások és a téma-váltó is.
        /// </summary>
        private void ResizeButtons()
        {
            int width = itemsPanel.ClientSize.Width - itemsPanel.Padding.Horizontal;

            int ideal = Ui.S(58);
            int minimum = Ui.S(40);
            int margin = Ui.S(8);
            int height = ideal;
            if (buttons.Count > 0 && itemsPanel.ClientSize.Height > 0)
            {
                // a görgetősáv megjelenése nem szűkíti a számítást: a teljes magasságból indulunk ki
                int available = Height - header.Height - userChip.Height - itemsPanel.Padding.Vertical;
                height = Math.Clamp(available / buttons.Count - margin, minimum, ideal);
            }

            foreach (var button in buttons.Values)
            {
                button.Width = Math.Max(width, 0);
                if (button.Height != height) button.Height = height;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tips.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>Logó (lekerekített csempe, benne T betű) és a kétsúlyú "TutorConnect" felirat.</summary>
        private class BrandHeader : Control
        {
            private bool collapsed;

            public BrandHeader()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            public void SetCollapsed(bool value)
            {
                collapsed = value;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Theme.NavBg);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                // lekerekített csempe színátmenettel; összecsukva középen, egyébként balra
                int tileSize = Ui.S(50);
                int tileX = collapsed ? (Width - tileSize) / 2 : Ui.S(22);
                var tile = new Rectangle(tileX, (Height - tileSize) / 2, tileSize, tileSize);

                using (var shadowPath = Ui.RoundedPath(new Rectangle(tile.X, tile.Y + Ui.S(3), tile.Width, tile.Height), Ui.S(16)))
                using (var shadow = new SolidBrush(Color.FromArgb(45, Color.Black)))
                    g.FillPath(shadow, shadowPath);

                using (var path = Ui.RoundedPath(tile, Ui.S(16)))
                using (var fill = new LinearGradientBrush(tile, Theme.White, Anim.Lerp(Theme.White, Theme.MainBlueDark, 0.28), 60F))
                    g.FillPath(fill, path);

                using (var logoBrush = new SolidBrush(Theme.MainBlueDark))
                using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("T", Ui.MakeFont(20F, FontStyle.Bold), logoBrush, tile, center);

                if (collapsed) return;

                // A felirat soha nem törik új sorba: ha nem fér el, a betűméret csökken.
                int textX = tile.Right + Ui.S(14);
                int textWidth = Math.Max(Width - textX - Ui.S(14), 0);
                float size = 16F;
                var light = Ui.MakeFont(size);
                var bold = Ui.MakeFont(size, FontStyle.Bold);
                while (size > 9F && Measure("Tutor", light) + Measure("Connect", bold) > textWidth)
                {
                    size -= 0.5F;
                    light = Ui.MakeFont(size);
                    bold = Ui.MakeFont(size, FontStyle.Bold);
                }

                int titleY = Height / 2 - Ui.S(25);
                var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                int lightWidth = Measure("Tutor", light);
                TextRenderer.DrawText(g, "Tutor", light, new Point(textX, titleY), Color.FromArgb(215, Theme.White), flags);
                TextRenderer.DrawText(g, "Connect", bold, new Point(textX + lightWidth, titleY), Theme.White, flags);

                TextRenderer.DrawText(g, "Tanulj és taníts", Ui.MakeFont(8.5F), new Point(textX, titleY + Ui.S(30)),
                    Color.FromArgb(150, Theme.White), flags);
            }

            private static int Measure(string text, Font font) =>
                TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        }

        /// <summary>Felhasználói kártya a navbár alján: avatar online-jelzéssel, név, szerepkör és nyíl.</summary>
        private class UserChip : Control
        {
            private string name = "";
            private string role = "";
            private bool collapsed;
            private bool hover;

            public UserChip()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
            }

            public void SetUser(string userName, string roleName)
            {
                name = userName;
                role = roleName;
                Invalidate();
            }

            public void SetCollapsed(bool value)
            {
                collapsed = value;
                Invalidate();
            }

            protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Theme.NavBg);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                // lebegő, áttetsző kártya (vonal nélkül)
                var card = new Rectangle(Ui.S(14), Ui.S(8), Math.Max(Width - Ui.S(28), 0), Math.Max(Height - Ui.S(24), 0));
                using (var path = Ui.RoundedPath(card, Ui.S(18)))
                {
                    using var fill = new SolidBrush(Color.FromArgb(hover ? 62 : 36, Theme.White));
                    using var border = new Pen(Color.FromArgb(hover ? 90 : 50, Theme.White));
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }

                int size = Ui.S(44);
                var avatar = new Rectangle(collapsed ? card.X + (card.Width - size) / 2 : card.X + Ui.S(12), card.Y + (card.Height - size) / 2, size, size);
                Ui.DrawAvatar(g, avatar, Ui.Initials(name), Theme.White, Theme.MainBlueDark);

                // online-jelzés
                int dot = Ui.S(12);
                var dotRect = new Rectangle(avatar.Right - dot + Ui.S(2), avatar.Bottom - dot + Ui.S(2), dot, dot);
                using (var ring = new SolidBrush(Theme.NavBg))
                    g.FillEllipse(ring, Rectangle.Inflate(dotRect, Ui.S(2), Ui.S(2)));
                using (var online = new SolidBrush(Theme.Success))
                    g.FillEllipse(online, dotRect);

                if (collapsed) return;

                int arrowWidth = Ui.S(22);
                int x = avatar.Right + Ui.S(12);
                int textWidth = Math.Max(card.Right - arrowWidth - x - Ui.S(4), 0);
                int midY = card.Y + card.Height / 2;

                using var nameFormat = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Far };
                using var nameBrush = new SolidBrush(Theme.White);
                g.DrawString(name, Ui.MakeFont(11F, FontStyle.Bold), nameBrush, new Rectangle(x, midY - Ui.S(24), textWidth, Ui.S(24)), nameFormat);

                using var roleFormat = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
                using var roleBrush = new SolidBrush(Color.FromArgb(185, Theme.White));
                g.DrawString(role, Ui.MakeFont(9F), roleBrush, new Rectangle(x, midY + Ui.S(1), textWidth, Ui.S(22)), roleFormat);

                // nyíl a kártya jobb szélén
                using var arrowBrush = new SolidBrush(Color.FromArgb(hover ? 230 : 140, Theme.White));
                using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("\uE76C", Ui.IconFont(10F), arrowBrush, new Rectangle(card.Right - arrowWidth - Ui.S(6), card.Y, arrowWidth, card.Height), center);
            }
        }

        /// <summary>Lekerekített, ikonos menügomb animált hover és aktív állapottal.</summary>
        private class NavButton : Control
        {
            private string icon;
            private bool collapsed;
            private double hoverT;
            private double activeT;
            private bool activeTarget;
            private int badge;
            private Anim.Tween? hoverTween;
            private Anim.Tween? activeTween;

            public NavButton(string text, string icon)
            {
                this.icon = icon;
                Text = text;
                Height = Ui.S(58);
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.Selectable, true); // billentyűzettel is bejárható és megnyitható
                TabStop = true;
                AccessibleRole = AccessibleRole.PushButton;
                AccessibleName = text;
            }

            public void SetCollapsed(bool value)
            {
                collapsed = value;
                Invalidate();
            }

            public void SetBadge(int count)
            {
                badge = Math.Max(count, 0);
                AccessibleDescription = badge > 0 ? $"{badge} új" : "";
                Invalidate();
            }

            public void SetContent(string text, string newIcon)
            {
                Text = text;
                AccessibleName = text;
                icon = newIcon;
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


            public void SetActive(bool value)
            {
                // a már beállított állapotot nem animáljuk újra (kattintáskor minden gombra meghívódik)
                if (value == activeTarget) return;
                activeTarget = value;

                activeTween?.Cancel();
                double from = activeT;
                double to = value ? 1 : 0;
                activeTween = Anim.Run(220, t =>
                {
                    activeT = from + (to - from) * t;
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

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Theme.NavBg);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                double amount = Math.Max(activeT, hoverT * 0.55);
                if (amount > 0.01)
                {
                    using var path = Ui.RoundedPath(bounds, Ui.S(14));
                    using var brush = new SolidBrush(Anim.Lerp(Theme.NavBg, Theme.NavActive, amount));
                    g.FillPath(brush, path);
                }

                // az aktív gomb bal oldali jelzősávja: a kijelölt elem teljes magasságán végigmegy,
                // követi a lekerekített sarkokat, és középről nőtt ki
                if (activeT > 0.01)
                {
                    int barHeight = (int)(Height * activeT);
                    using var shape = Ui.RoundedPath(bounds, Ui.S(14));
                    using var bar = new SolidBrush(Theme.White);
                    g.SetClip(shape);
                    g.FillRectangle(bar, 0, (Height - barHeight) / 2, Ui.S(6), barHeight);
                    g.ResetClip();
                }

                if (Focused && ShowFocusCues)
                {
                    using var focusPen = new Pen(Theme.HighContrast ? Color.Yellow : Color.White, Ui.S(2F)) { DashStyle = DashStyle.Dot };
                    using var focusPath = Ui.RoundedPath(Rectangle.Inflate(bounds, -Ui.S(3), -Ui.S(3)), Ui.S(11));
                    g.DrawPath(focusPen, focusPath);
                }

                Color textColor = Anim.Lerp(Color.FromArgb(215, 255, 255, 255), Color.White, Math.Max(activeT, hoverT));
                using var textBrush = new SolidBrush(textColor);

                if (collapsed)
                {
                    // csak ikon (vagy az első betű, ha nincs ikon), középre igazítva
                    using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    var glyphFont = icon.Length > 0
                        ? Ui.IconFont(17F)
                        : Ui.MakeFont(14F, FontStyle.Bold);
                    string glyph = icon.Length > 0 ? icon : Text[..1];
                    g.DrawString(glyph, glyphFont, textBrush, new Rectangle(0, 0, Width, Height), centered);

                    // összecsukva a jelvény egy kis piros pont az ikon sarkán
                    if (badge > 0)
                    {
                        using var dot = new SolidBrush(Theme.Danger);
                        using var ring = new Pen(Theme.NavBg, Ui.S(2F));
                        var dotRect = new Rectangle(Width / 2 + Ui.S(6), Ui.S(10), Ui.S(12), Ui.S(12));
                        g.FillEllipse(dot, dotRect);
                        g.DrawEllipse(ring, dotRect);
                    }
                    return;
                }

                using var middle = new StringFormat { LineAlignment = StringAlignment.Center };
                if (icon.Length > 0)
                {
                    var iconFont = Ui.IconFont(17F);
                    g.DrawString(icon, iconFont, textBrush, new Rectangle(Ui.S(22), 0, Ui.S(40), Height), middle);
                }

                // a vastagság a cél-állapotot követi, így az animáció közben nem ugrál a szöveg szélessége
                var font = Ui.MakeFont(12.5F, activeTarget ? FontStyle.Bold : FontStyle.Regular);
                using var textFormat = new StringFormat(StringFormatFlags.NoWrap)
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                g.DrawString(Text, font, textBrush, new Rectangle(icon.Length > 0 ? Ui.S(70) : Ui.S(24), 0, Math.Max(Width - Ui.S(76) - (badge > 0 ? Ui.S(44) : 0), 0), Height), textFormat);

                // jelvény: piros pirula a gomb jobb szélén
                if (badge > 0)
                {
                    string count = badge > 99 ? "99+" : badge.ToString();
                    var badgeFont = Ui.MakeFont(9F, FontStyle.Bold);
                    int badgeWidth = Math.Max(Ui.S(26), TextRenderer.MeasureText(count, badgeFont).Width + Ui.S(14));
                    var pill = new Rectangle(Width - Ui.S(16) - badgeWidth, (Height - Ui.S(26)) / 2, badgeWidth, Ui.S(26));
                    using var pillPath = Ui.RoundedPath(pill, Ui.S(13));
                    using var pillBrush = new SolidBrush(Theme.Danger);
                    g.FillPath(pillBrush, pillPath);
                    using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    using var white = new SolidBrush(Theme.White);
                    g.DrawString(count, badgeFont, white, pill, center);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    hoverTween?.Cancel();
                    activeTween?.Cancel();
                }
                base.Dispose(disposing);
            }
        }
    }
}

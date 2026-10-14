using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>
    /// Bal oldali navigációs sáv. Menüpont (kijelölhető oldal): AddItem("kulcs", "Felirat", "").
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
            Width = 250; // az ApplyResponsive() később az ablakhoz igazítja
            BackColor = Theme.NavBg;

            itemsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(12, 10, 12, 0),
                BackColor = Theme.NavBg
            };
            itemsPanel.Resize += (_, _) => ResizeButtons();

            header = new BrandHeader { Dock = DockStyle.Top, Height = 96 };

            userChip = new UserChip { Dock = DockStyle.Bottom, Height = 76 };
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
            var button = new NavButton(text, icon) { Margin = new Padding(0, 0, 0, 6) };
            button.SetCollapsed(collapsed);
            buttons[key] = button;
            itemsPanel.Controls.Add(button);
            ResizeButtons();
            return button;
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

            Width = collapsed ? 78 : Math.Clamp((int)(availableWidth * 0.2), 230, 290);
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

        private void ResizeButtons()
        {
            int width = itemsPanel.ClientSize.Width - itemsPanel.Padding.Horizontal;
            foreach (var button in buttons.Values)
                button.Width = Math.Max(width, 0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tips.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>Logó (kör, benne T betű) és az alkalmazás neve.</summary>
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

                // összecsukva a logó középen, egyébként balra
                int circleX = collapsed ? (Width - 44) / 2 : 24;
                var circle = new Rectangle(circleX, (Height - 44) / 2, 44, 44);
                using (var fill = new SolidBrush(Theme.White))
                    g.FillEllipse(fill, circle);

                using (var logoFont = new Font(Theme.FontFamily, 18F, FontStyle.Bold))
                using (var logoBrush = new SolidBrush(Theme.MainBlueDark))
                using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("T", logoFont, logoBrush, circle, center);

                if (!collapsed)
                {
                    // A cím soha nem törik új sorba: ha nem fér el, a betűméret csökken.
                    var textArea = new Rectangle(80, 0, Math.Max(Width - 80 - 12, 0), Height);
                    float size = 15F;
                    Font titleFont = new(Theme.FontFamily, size, FontStyle.Bold);
                    while (size > 9F && TextRenderer.MeasureText("TutorConnect", titleFont).Width > textArea.Width)
                    {
                        titleFont.Dispose();
                        size -= 0.5F;
                        titleFont = new Font(Theme.FontFamily, size, FontStyle.Bold);
                    }

                    using (titleFont)
                    using (var format = new StringFormat(StringFormatFlags.NoWrap)
                    {
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    })
                    using (var titleBrush = new SolidBrush(Theme.White))
                        g.DrawString("TutorConnect", titleFont, titleBrush, textArea, format);
                }

                // vékony elválasztó vonal a fejléc alatt
                using var line = new Pen(Color.FromArgb(55, Theme.White));
                g.DrawLine(line, 16, Height - 1, Width - 16, Height - 1);
            }
        }

        /// <summary>Felhasználói blokk a navbár alján: avatar, név és szerepkör.</summary>
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

                if (hover)
                {
                    using var path = Ui.RoundedPath(new Rectangle(10, 8, Width - 21, Height - 17), 12);
                    using var hoverBrush = new SolidBrush(Color.FromArgb(110, Theme.NavActive));
                    g.FillPath(hoverBrush, path);
                }

                using (var line = new Pen(Color.FromArgb(55, Theme.White)))
                    g.DrawLine(line, 16, 0, Width - 16, 0);

                const int size = 40;
                var avatar = new Rectangle(collapsed ? (Width - size) / 2 : 22, (Height - size) / 2, size, size);
                Ui.DrawAvatar(g, avatar, Ui.Initials(name), Theme.White, Theme.MainBlueDark);

                if (collapsed) return;

                int x = avatar.Right + 12;
                int textWidth = Math.Max(Width - x - 12, 0);
                using var nameFormat = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Far };
                using var nameFont = new Font(Theme.FontFamily, 10F, FontStyle.Bold);
                using var nameBrush = new SolidBrush(Theme.White);
                g.DrawString(name, nameFont, nameBrush, new Rectangle(x, Height / 2 - 22, textWidth, 22), nameFormat);

                using var roleFormat = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
                using var roleFont = new Font(Theme.FontFamily, 8.5F);
                using var roleBrush = new SolidBrush(Color.FromArgb(200, Theme.White));
                g.DrawString(role, roleFont, roleBrush, new Rectangle(x, Height / 2 + 1, textWidth, 20), roleFormat);
            }
        }

        /// <summary>Lekerekített, ikonos menügomb animált hover és aktív állapottal.</summary>
        private class NavButton : Control
        {
            private string icon;
            private bool collapsed;
            private double hoverT;
            private double activeT;
            private Anim.Tween? hoverTween;
            private Anim.Tween? activeTween;

            public NavButton(string text, string icon)
            {
                this.icon = icon;
                Text = text;
                Height = 48;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.Selectable, false);
            }

            public void SetCollapsed(bool value)
            {
                collapsed = value;
                Invalidate();
            }

            public void SetContent(string text, string newIcon)
            {
                Text = text;
                icon = newIcon;
                Invalidate();
            }

            public void SetActive(bool value)
            {
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
                    using var path = Ui.RoundedPath(bounds, 12);
                    using var brush = new SolidBrush(Anim.Lerp(Theme.NavBg, Theme.NavActive, amount));
                    g.FillPath(brush, path);
                }

                // az aktív gomb bal oldali jelzősávja nő be
                if (activeT > 0.01)
                {
                    int barHeight = (int)(24 * activeT);
                    using var bar = new SolidBrush(Theme.White);
                    g.FillRectangle(bar, 0, (Height - barHeight) / 2, 4, barHeight);
                }

                Color textColor = Anim.Lerp(Color.FromArgb(215, 255, 255, 255), Color.White, Math.Max(activeT, hoverT));
                using var textBrush = new SolidBrush(textColor);

                if (collapsed)
                {
                    // csak ikon (vagy az első betű, ha nincs ikon), középre igazítva
                    using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    using var glyphFont = icon.Length > 0
                        ? new Font("Segoe MDL2 Assets", 14F)
                        : new Font(Theme.FontFamily, 12F, FontStyle.Bold);
                    string glyph = icon.Length > 0 ? icon : Text[..1];
                    g.DrawString(glyph, glyphFont, textBrush, new Rectangle(0, 0, Width, Height), centered);
                    return;
                }

                using var middle = new StringFormat { LineAlignment = StringAlignment.Center };
                if (icon.Length > 0)
                {
                    using var iconFont = new Font("Segoe MDL2 Assets", 14F);
                    g.DrawString(icon, iconFont, textBrush, new Rectangle(18, 0, 34, Height), middle);
                }

                using var font = new Font(Theme.FontFamily, 11F, activeT > 0.5 ? FontStyle.Bold : FontStyle.Regular);
                using var textFormat = new StringFormat(StringFormatFlags.NoWrap)
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                g.DrawString(Text, font, textBrush, new Rectangle(icon.Length > 0 ? 58 : 20, 0, Math.Max(Width - 62, 0), Height), textFormat);
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

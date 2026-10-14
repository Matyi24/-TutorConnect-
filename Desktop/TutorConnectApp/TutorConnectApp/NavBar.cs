using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace TutorConnectApp
{
    /// <summary>
    /// Bal oldali navigációs sáv. Új menüpont: AddItem("kulcs", "Felirat", "").
    /// Az ikon a Segoe MDL2 Assets betűkészlet egy karaktere (opcionális).
    /// </summary>
    public class NavBar : UserControl
    {
        private readonly BrandHeader header;
        private readonly UserChip userChip;
        private readonly FlowLayoutPanel itemsPanel;
        private readonly Dictionary<string, NavButton> buttons = new();

        /// <summary>Menüpontra kattintáskor fut le, a paraméter a menüpont kulcsa.</summary>
        public event EventHandler<string>? ItemClicked;

        public string? ActiveKey { get; private set; }

        public NavBar()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Left;
            Width = 250; // az ApplyResponsive() később az ablakhoz igazítja
            BackColor = Theme.MainBlueDark;

            itemsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(12, 8, 12, 0),
                BackColor = Theme.MainBlueDark
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
            var button = new NavButton(text, icon) { Margin = new Padding(0, 0, 0, 6) };
            button.Click += (_, _) => Select(key);
            buttons[key] = button;
            itemsPanel.Controls.Add(button);
            ResizeButtons();
        }

        public void Select(string key)
        {
            if (!buttons.ContainsKey(key)) return;

            ActiveKey = key;
            foreach (var pair in buttons)
                pair.Value.Active = pair.Key == key;

            ItemClicked?.Invoke(this, key);
        }

        /// <summary>
        /// Reszponzív méretezés: az ablak szélességétől függően a sáv szélessége változik,
        /// keskeny ablaknál csak az ikonok látszanak (összecsukott mód).
        /// </summary>
        public void ApplyResponsive(int availableWidth)
        {
            bool collapsed = availableWidth < 800;
            Width = collapsed ? 76 : Math.Clamp((int)(availableWidth * 0.2), 220, 280);
            header.Collapsed = collapsed;
            userChip.SetCollapsed(collapsed);
            foreach (var button in buttons.Values)
                button.Collapsed = collapsed;
            ResizeButtons();
        }

        private void ResizeButtons()
        {
            int width = itemsPanel.ClientSize.Width - itemsPanel.Padding.Horizontal;
            foreach (var button in buttons.Values)
                button.Width = Math.Max(width, 0);
        }

        /// <summary>Logó (kör, benne T betű) és az alkalmazás neve.</summary>
        private class BrandHeader : Control
        {
            private bool collapsed;

            [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public bool Collapsed
            {
                get => collapsed;
                set { collapsed = value; Invalidate(); }
            }

            public BrandHeader()
            {
                DoubleBuffered = true;
                BackColor = Theme.MainBlueDark;
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // összecsukva a logó középen, egyébként balra
                int circleX = collapsed ? (Width - 44) / 2 : 24;
                var circle = new Rectangle(circleX, (Height - 44) / 2, 44, 44);
                using (var fill = new SolidBrush(Theme.White))
                    g.FillEllipse(fill, circle);

                using var logoFont = new Font(Theme.FontFamily, 18F, FontStyle.Bold);
                using var logoBrush = new SolidBrush(Theme.MainBlueDark);
                var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("T", logoFont, logoBrush, circle, center);

                if (!collapsed)
                {
                    // A cím soha nem törik új sorba: ha nem fér el, a betűméret csökken.
                    var textArea = new Rectangle(80, 0, Width - 80 - 12, Height);
                    float size = 15F;
                    Font titleFont = new(Theme.FontFamily, size, FontStyle.Bold);
                    while (size > 9F && TextRenderer.MeasureText("TutorConnect", titleFont).Width > textArea.Width)
                    {
                        titleFont.Dispose();
                        size -= 0.5F;
                        titleFont = new Font(Theme.FontFamily, size, FontStyle.Bold);
                    }

                    using (titleFont)
                    {
                        var format = new StringFormat(StringFormatFlags.NoWrap)
                        {
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter
                        };
                        using var titleBrush = new SolidBrush(Theme.White);
                        g.DrawString("TutorConnect", titleFont, titleBrush, textArea, format);
                    }
                }

                // vékony elválasztó vonal a fejléc alatt
                using var line = new Pen(Color.FromArgb(60, Theme.White));
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
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
                BackColor = Theme.MainBlueDark;
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
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                if (hover)
                {
                    using var path = Ui.RoundedPath(new Rectangle(10, 8, Width - 21, Height - 17), 12);
                    using var hoverBrush = new SolidBrush(Color.FromArgb(90, Theme.MainBlue));
                    g.FillPath(hoverBrush, path);
                }

                using (var line = new Pen(Color.FromArgb(60, Theme.White)))
                    g.DrawLine(line, 16, 0, Width - 16, 0);

                const int size = 40;
                var avatar = new Rectangle(collapsed ? (Width - size) / 2 : 22, (Height - size) / 2, size, size);
                Ui.DrawAvatar(g, avatar, Ui.Initials(name), Theme.White, Theme.MainBlueDark);

                if (collapsed) return;

                int x = avatar.Right + 12;
                int textWidth = Math.Max(Width - x - 12, 0);
                var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Far };
                using var nameFont = new Font(Theme.FontFamily, 10F, FontStyle.Bold);
                using var nameBrush = new SolidBrush(Theme.White);
                g.DrawString(name, nameFont, nameBrush, new Rectangle(x, Height / 2 - 22, textWidth, 22), format);

                var roleFormat = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
                using var roleFont = new Font(Theme.FontFamily, 8.5F);
                using var roleBrush = new SolidBrush(Color.FromArgb(200, Theme.White));
                g.DrawString(role, roleFont, roleBrush, new Rectangle(x, Height / 2 + 1, textWidth, 20), roleFormat);
            }
        }

        /// <summary>Lekerekített, ikonos menügomb hover és aktív állapottal.</summary>
        private class NavButton : Control
        {
            private readonly string icon;
            private bool hover;
            private bool active;
            private bool collapsed;

            [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public bool Active
            {
                get => active;
                set { active = value; Invalidate(); }
            }

            /// <summary>Összecsukott módban csak az ikon látszik, középen.</summary>
            [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public bool Collapsed
            {
                get => collapsed;
                set { collapsed = value; Invalidate(); }
            }

            public NavButton(string text, string icon)
            {
                this.icon = icon;
                Text = text;
                Height = 48;
                Cursor = Cursors.Hand;
                DoubleBuffered = true;
                SetStyle(ControlStyles.Selectable, false);
                BackColor = Theme.MainBlueDark;
            }

            protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                if (active || hover)
                {
                    var color = active ? Theme.MainBlue : Color.FromArgb(90, Theme.MainBlue);
                    using var path = RoundedRect(bounds, 12);
                    using var brush = new SolidBrush(color);
                    g.FillPath(brush, path);
                }

                var middle = new StringFormat { LineAlignment = StringAlignment.Center };
                using var textBrush = new SolidBrush(Theme.White);

                if (collapsed)
                {
                    // csak ikon (vagy az első betű, ha nincs ikon), középre igazítva
                    var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    using var iconFont = icon.Length > 0
                        ? new Font("Segoe MDL2 Assets", 14F)
                        : new Font(Theme.FontFamily, 12F, FontStyle.Bold);
                    string glyph = icon.Length > 0 ? icon : Text[..1];
                    g.DrawString(glyph, iconFont, textBrush, new Rectangle(0, 0, Width, Height), centered);
                    return;
                }

                if (icon.Length > 0)
                {
                    using var iconFont = new Font("Segoe MDL2 Assets", 14F);
                    g.DrawString(icon, iconFont, textBrush, new Rectangle(16, 0, 32, Height), middle);
                }

                using var font = new Font(Theme.FontFamily, 11F, active ? FontStyle.Bold : FontStyle.Regular);
                var textFormat = new StringFormat(StringFormatFlags.NoWrap)
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                g.DrawString(Text, font, textBrush, new Rectangle(icon.Length > 0 ? 56 : 20, 0, Width - 60, Height), textFormat);
            }

            private static GraphicsPath RoundedRect(Rectangle r, int radius)
            {
                int d = radius * 2;
                var path = new GraphicsPath();
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}

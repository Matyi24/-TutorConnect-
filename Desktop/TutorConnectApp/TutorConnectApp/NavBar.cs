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
        private readonly FlowLayoutPanel itemsPanel;
        private readonly Dictionary<string, NavButton> buttons = new();

        /// <summary>Menüpontra kattintáskor fut le, a paraméter a menüpont kulcsa.</summary>
        public event EventHandler<string>? ItemClicked;

        public string? ActiveKey { get; private set; }

        public NavBar()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Left;
            Width = 250;
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

            Controls.Add(itemsPanel);
            Controls.Add(header);
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

        private void ResizeButtons()
        {
            int width = itemsPanel.ClientSize.Width - itemsPanel.Padding.Horizontal;
            foreach (var button in buttons.Values)
                button.Width = Math.Max(width, 0);
        }

        /// <summary>Logó (kör, benne T betű) és az alkalmazás neve.</summary>
        private class BrandHeader : Control
        {
            public BrandHeader()
            {
                DoubleBuffered = true;
                BackColor = Theme.MainBlueDark;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var circle = new Rectangle(24, (Height - 44) / 2, 44, 44);
                using (var fill = new SolidBrush(Theme.White))
                    g.FillEllipse(fill, circle);

                using var logoFont = new Font(Theme.FontFamily, 18F, FontStyle.Bold);
                using var logoBrush = new SolidBrush(Theme.MainBlueDark);
                var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("T", logoFont, logoBrush, circle, center);

                using var titleFont = new Font(Theme.FontFamily, 15F, FontStyle.Bold);
                using var titleBrush = new SolidBrush(Theme.White);
                var textArea = new Rectangle(80, 0, Width - 80, Height);
                var left = new StringFormat { LineAlignment = StringAlignment.Center };
                g.DrawString("TutorConnect", titleFont, titleBrush, textArea, left);

                // vékony elválasztó vonal a fejléc alatt
                using var line = new Pen(Color.FromArgb(60, Theme.White));
                g.DrawLine(line, 16, Height - 1, Width - 16, Height - 1);
            }
        }

        /// <summary>Lekerekített, ikonos menügomb hover és aktív állapottal.</summary>
        private class NavButton : Control
        {
            private readonly string icon;
            private bool hover;
            private bool active;

            public bool Active
            {
                get => active;
                set { active = value; Invalidate(); }
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

                if (icon.Length > 0)
                {
                    using var iconFont = new Font("Segoe MDL2 Assets", 14F);
                    g.DrawString(icon, iconFont, textBrush, new Rectangle(16, 0, 32, Height), middle);
                }

                using var font = new Font(Theme.FontFamily, 11F, active ? FontStyle.Bold : FontStyle.Regular);
                g.DrawString(Text, font, textBrush, new Rectangle(icon.Length > 0 ? 56 : 20, 0, Width - 60, Height), middle);
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

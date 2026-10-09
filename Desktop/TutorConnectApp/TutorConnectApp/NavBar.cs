namespace TutorConnectApp
{
    /// <summary>
    /// Bal oldali navigációs sáv. Új menüpont: AddItem("kulcs", "Felirat").
    /// </summary>
    public class NavBar : UserControl
    {
        private readonly Label titleLabel;
        private readonly FlowLayoutPanel itemsPanel;
        private readonly Dictionary<string, NavButton> buttons = new();

        /// <summary>Menüpontra kattintáskor fut le, a paraméter a menüpont kulcsa.</summary>
        public event EventHandler<string>? ItemClicked;

        public string? ActiveKey { get; private set; }

        public NavBar()
        {
            Dock = DockStyle.Left;
            Width = 220;
            BackColor = Theme.MainBlueDark;

            itemsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = Theme.MainBlueDark
            };

            titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 80,
                Text = "TutorConnect",
                ForeColor = Theme.White,
                Font = new Font(Theme.FontFamily, 16F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Controls.Add(itemsPanel);
            Controls.Add(titleLabel);
        }

        public void AddItem(string key, string text)
        {
            var button = new NavButton(text)
            {
                Width = Width,
                Margin = new Padding(0)
            };
            button.Click += (_, _) => Select(key);
            buttons[key] = button;
            itemsPanel.Controls.Add(button);
        }

        public void Select(string key)
        {
            if (!buttons.ContainsKey(key)) return;

            ActiveKey = key;
            foreach (var pair in buttons)
                pair.Value.Active = pair.Key == key;

            ItemClicked?.Invoke(this, key);
        }

        /// <summary>Navbár gomb: lapos, hover és aktív állapottal, bal oldali csíkkal.</summary>
        private class NavButton : Button
        {
            private bool active;

            public bool Active
            {
                get => active;
                set
                {
                    active = value;
                    BackColor = active ? Theme.MainBlue : Theme.MainBlueDark;
                    Invalidate();
                }
            }

            public NavButton(string text)
            {
                Text = text;
                Height = 48;
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                FlatAppearance.MouseOverBackColor = Theme.MainBlue;
                FlatAppearance.MouseDownBackColor = Theme.MainBlue;
                BackColor = Theme.MainBlueDark;
                ForeColor = Theme.White;
                Font = new Font(Theme.FontFamily, 11F);
                TextAlign = ContentAlignment.MiddleLeft;
                Padding = new Padding(24, 0, 0, 0);
                Cursor = Cursors.Hand;
                TabStop = false;
            }

            protected override void OnPaint(PaintEventArgs pevent)
            {
                base.OnPaint(pevent);
                if (active)
                {
                    using var brush = new SolidBrush(Theme.White);
                    pevent.Graphics.FillRectangle(brush, 0, 0, 4, Height);
                }
            }
        }
    }
}

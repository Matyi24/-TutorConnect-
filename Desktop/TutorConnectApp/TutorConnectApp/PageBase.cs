namespace TutorConnectApp
{
    /// <summary>
    /// Egy oldal alapja: fejléc (cím + alcím) és alatta görgethető tartalom.
    /// Az utódok a BuildAsync-ben töltik fel a tartalmat (AddRow); a szélességet, a betöltésjelzőt,
    /// a hibakezelést és a sorok megjelenési animációját ez az osztály kezeli.
    /// </summary>
    internal abstract class PageBase : UserControl
    {
        protected readonly FlowLayoutPanel Body;
        private bool fitting;
        private int revealIndex;

        /// <summary>Az oldal navigációt kér (pl. a főoldali gomb az Oktatók oldalra visz). Paraméter: a menüpont kulcsa.</summary>
        public event Action<string>? NavigateRequested;

        protected PageBase(string title, string subtitle)
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.PageBg;
            DoubleBuffered = true;

            Body = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(36, 4, 36, 28),
                BackColor = Theme.PageBg
            };
            Body.ClientSizeChanged += (_, _) => FitAll();

            var header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Theme.PageBg };
            var titleLabel = Ui.Lbl(title, 23F, Theme.Text, bold: true);
            titleLabel.Location = new Point(33, 20);
            var subtitleLabel = Ui.Lbl(subtitle, 10.5F, Theme.Muted);
            subtitleLabel.Location = new Point(37, 62);
            header.Controls.Add(titleLabel);
            header.Controls.Add(subtitleLabel);

            Controls.Add(Body);   // a kitöltő (Fill) elem legyen elöl a sorrendben
            Controls.Add(header);
        }

        protected void Navigate(string key) => NavigateRequested?.Invoke(key);

        /// <summary>Betölti az oldal tartalmát; hiba esetén hibakártyát mutat újrapróbálás gombbal.</summary>
        public async Task LoadAsync()
        {
            ClearBody();
            AddRow(Ui.Loading(), animate: false);
            try
            {
                await BuildAsync();
            }
            catch (ApiException ex)
            {
                if (!IsDisposed) ShowError(ex.Message);
            }
            catch (Exception ex) when (ex is not ObjectDisposedException)
            {
                if (!IsDisposed) ShowError("Váratlan hiba: " + ex.Message);
            }
        }

        /// <summary>Az adatok lekérése után hívd a ClearBody()-t, majd add hozzá a sorokat az AddRow-val.</summary>
        protected abstract Task BuildAsync();

        /// <summary>Új sor a tartalomhoz. Az animate sorok sorban, lentről felcsúszva jelennek meg.</summary>
        protected void AddRow(Control control, bool animate = true)
        {
            FitWidth(control);

            if (!animate || !Anim.Enabled)
            {
                Body.Controls.Add(control);
                return;
            }

            int index = revealIndex++;
            Padding margin = control.Margin;
            const int Offset = 28;

            control.Visible = false;
            Body.Controls.Add(control);

            Anim.Delay(Math.Min(index, 8) * 55, () =>
            {
                if (control.IsDisposed) return;

                control.Margin = new Padding(margin.Left, margin.Top + Offset, margin.Right, margin.Bottom);
                control.Visible = true;

                Anim.Run(360, t =>
                {
                    if (control.IsDisposed) return;
                    control.Margin = new Padding(margin.Left, margin.Top + (int)(Offset * (1 - t)), margin.Right, margin.Bottom);
                    if (control is CardPanel card) card.SetReveal(t);
                }, () =>
                {
                    if (control.IsDisposed) return;
                    control.Margin = margin;
                    if (control is CardPanel card) card.SetReveal(1);
                });
            });
        }

        protected void ClearBody()
        {
            revealIndex = 0;
            foreach (var control in Body.Controls.Cast<Control>().ToList())
            {
                Body.Controls.Remove(control);
                control.Dispose();
            }
        }

        /// <summary>Az anchor utáni összes sor törlése (lista újratöltéséhez).</summary>
        protected void RemoveRowsAfter(Control anchor)
        {
            revealIndex = 0;
            int index = Body.Controls.IndexOf(anchor);
            if (index < 0) return;

            var rows = Body.Controls.Cast<Control>().Skip(index + 1).ToList();
            foreach (var control in rows)
            {
                Body.Controls.Remove(control);
                control.Dispose();
            }
        }

        private void ShowError(string message)
        {
            ClearBody();
            var card = new CardPanel();
            card.Controls.Add(Ui.Lbl("Nem sikerült betölteni", 12F, Theme.Danger, bold: true));
            card.Controls.Add(Ui.Lbl(message, 10.5F, Theme.Muted, wrap: true));
            card.Controls.Add(Ui.PrimaryBtn("Újrapróbálás", (_, _) => BeginInvoke(new Action(() => _ = LoadAsync()))));
            AddRow(card);
        }

        private void FitAll()
        {
            if (fitting) return;
            fitting = true;
            try
            {
                foreach (Control control in Body.Controls)
                    FitWidth(control);
            }
            finally
            {
                fitting = false;
            }
        }

        private void FitWidth(Control control)
        {
            int width = Math.Max(Body.ClientSize.Width - Body.Padding.Horizontal, 120);
            if (control.AutoSize)
            {
                // automatikus magasság, rögzített szélesség
                control.MinimumSize = new Size(width, 0);
                control.MaximumSize = new Size(width, 0);
            }
            else
            {
                control.Width = width;
            }
        }
    }
}

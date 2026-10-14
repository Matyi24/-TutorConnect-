namespace TutorConnectApp
{
    /// <summary>
    /// Egy oldal alapja: fejléc (cím + alcím) és alatta sima görgetésű tartalom.
    /// Az utódok a BuildAsync-ben töltik fel a tartalmat (AddRow); a szélességet, a betöltésjelzőt,
    /// a hibakezelést és a sorok megjelenési animációját ez az osztály kezeli.
    /// </summary>
    internal abstract class PageBase : UserControl
    {
        protected readonly FlowLayoutPanel Body;
        private readonly SmoothScrollPanel scroll;
        private int lastFitWidth = -1;
        private int revealIndex;

        /// <summary>Az oldal navigációt kér (pl. a főoldali gomb az Oktatók oldalra visz). Paraméter: a menüpont kulcsa.</summary>
        public event Action<string>? NavigateRequested;

        /// <summary>Az oldal kijelentkezést kér (a Profil oldal gombja).</summary>
        public event Action? LogoutRequested;

        protected PageBase(string title, string subtitle)
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.PageBg;
            DoubleBuffered = true;

            scroll = new SmoothScrollPanel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };
            Body = scroll.Content;
            Body.Padding = new Padding(36, 4, 22, 28);
            Body.ClientSizeChanged += (_, _) => FitAll();

            var header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Theme.PageBg };
            var titleLabel = Ui.Lbl(title, 23F, Theme.Text, bold: true);
            titleLabel.Location = new Point(33, 20);
            var subtitleLabel = Ui.Lbl(subtitle, 10.5F, Theme.Muted);
            subtitleLabel.Location = new Point(37, 62);
            header.Controls.Add(titleLabel);
            header.Controls.Add(subtitleLabel);

            Controls.Add(scroll);  // a kitöltő (Fill) elem legyen elöl a sorrendben
            Controls.Add(header);
        }

        protected void Navigate(string key) => NavigateRequested?.Invoke(key);

        protected void RequestLogout() => LogoutRequested?.Invoke();

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

        /// <summary>
        /// Új sor a tartalomhoz. Az animate sorok egymás után "jelennek meg" (a kártyák háttere halványul be);
        /// a már látható sorok helye közben nem változik, ezért nincs ugrálás.
        /// </summary>
        protected void AddRow(Control control, bool animate = true)
        {
            FitWidth(control);

            if (!animate || !Anim.Enabled)
            {
                Body.Controls.Add(control);
                return;
            }

            int index = revealIndex++;
            var card = control as CardPanel;
            card?.SetReveal(0);

            control.Visible = false;
            Body.Controls.Add(control);

            Anim.Delay(Math.Min(index, 8) * 60, () =>
            {
                if (control.IsDisposed) return;
                control.Visible = true;
                if (card != null)
                    Anim.Run(320, t => card.SetReveal(t));
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

        private int ContentWidth => Math.Max(Body.ClientSize.Width - Body.Padding.Horizontal, 120);

        private void FitAll()
        {
            // a tartalom magasságának változása (új sor) nem érinti a szélességet
            int width = ContentWidth;
            if (width == lastFitWidth) return;
            lastFitWidth = width;

            foreach (Control control in Body.Controls)
                FitWidth(control);
        }

        private void FitWidth(Control control)
        {
            int width = ContentWidth;
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

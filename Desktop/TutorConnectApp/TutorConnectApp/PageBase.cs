namespace TutorConnectApp
{
    /// <summary>
    /// Egy oldal alapja: fejléc (cím + alcím) és alatta görgethető tartalom.
    /// Az utódok a BuildAsync-ben töltik fel a tartalmat (AddRow), a szélességet ez az osztály kezeli.
    /// </summary>
    internal abstract class PageBase : UserControl
    {
        protected readonly FlowLayoutPanel Body;
        private bool fitting;

        protected PageBase(string title, string subtitle)
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.MainBlueLight;
            DoubleBuffered = true;

            Body = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(32, 4, 32, 24),
                BackColor = Theme.MainBlueLight
            };
            Body.ClientSizeChanged += (_, _) => FitAll();

            var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.MainBlueLight };
            var titleLabel = Ui.Lbl(title, 22F, Theme.DarkText, bold: true);
            titleLabel.Location = new Point(30, 18);
            var subtitleLabel = Ui.Lbl(subtitle, 10.5F, Ui.Muted);
            subtitleLabel.Location = new Point(33, 58);
            header.Controls.Add(titleLabel);
            header.Controls.Add(subtitleLabel);

            Controls.Add(Body);   // a kitöltő (Fill) elem legyen elöl a sorrendben
            Controls.Add(header);
        }

        /// <summary>Betölti az oldal tartalmát; hiba esetén hibakártyát mutat újrapróbálás gombbal.</summary>
        public async Task LoadAsync()
        {
            ClearBody();
            AddRow(Ui.Lbl("Betöltés...", 10.5F, Ui.Muted));
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

        protected void AddRow(Control control)
        {
            FitWidth(control);
            Body.Controls.Add(control);
        }

        protected void ClearBody()
        {
            foreach (var control in Body.Controls.Cast<Control>().ToList())
            {
                Body.Controls.Remove(control);
                control.Dispose();
            }
        }

        /// <summary>Az anchor utáni összes sor törlése (lista újratöltéséhez).</summary>
        protected void RemoveRowsAfter(Control anchor)
        {
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
            card.Controls.Add(Ui.Lbl("Nem sikerült betölteni", 12F, Ui.Danger, bold: true));
            card.Controls.Add(Ui.Lbl(message, 10.5F, Ui.Muted, wrap: true));
            card.Controls.Add(Ui.Btn("Újrapróbálás", Theme.MainBlue, Theme.White,
                (_, _) => BeginInvoke(new Action(() => _ = LoadAsync()))));
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

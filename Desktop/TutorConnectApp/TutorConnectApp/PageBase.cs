namespace TutorConnectApp
{
    /// <summary>
    /// Egy oldal alapja: fejléc (cím + alcím) és alatta sima görgetésű tartalom.
    /// Az utódok a BuildAsync-ben töltik fel a tartalmat (AddRow); a szélességet, a betöltésjelzőt és a hibakezelést
    /// ez az osztály kezeli. A tartalom egyben jelenik meg (nincs soronkénti animáció), a betöltésjelző csak lassú
    /// betöltésnél látszik, így gyors betöltésnél nincs villanás.
    /// </summary>
    internal abstract class PageBase : UserControl
    {
        protected readonly FlowLayoutPanel Body;

        /// <summary>Nem görgetett oldalaknál (pl. Chat) a teljes oldalt kitöltő felület.</summary>
        protected readonly Panel Surface = new() { Dock = DockStyle.Fill };

        private readonly SmoothScrollPanel scroll;
        private readonly Debouncer loaderDelay;
        private Control? loaderRow;
        private bool layoutHeld;
        private bool redrawHeld;
        private bool loading;
        private bool staleMarked;
        private long loadedTick;

        /// <summary>Az oldal tartalma sikeresen betöltődött (az oldal a főablakban megmarad, és újra használható).</summary>
        public bool Loaded { get; private set; }

        public bool Loading => loading;

        /// <summary>Az a menüpont, amelyre a felhasználó éppen navigált (a betöltés alatt is).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Igaz, ha egy másik oldal olyan kérést hagyott, amit csak újraépítés teljesít (pl. szűrés egy tantárgyra).</summary>
        public virtual bool HasPendingRequest => false;

        /// <summary>Igaz, ha az oldal magától frissül (valós idejű események), ezért nem kell időnként újraépíteni.</summary>
        public virtual bool SelfRefreshing => false;

        /// <summary>Igaz, ha ennek az oldalnak a műveletei más oldalak adatait is megváltoztatják.</summary>
        public virtual bool InvalidatesOthers => false;

        protected virtual int MaxAgeMs => 45_000;

        /// <summary>Visszatéréskor az oldal tartalma még látszik; ennyi idő után a háttérben frissül.</summary>
        public bool IsStale => !SelfRefreshing && (staleMarked || Environment.TickCount64 - loadedTick > MaxAgeMs);

        public void MarkStale() => staleMarked = true;

        internal void SetActive(bool active)
        {
            if (IsActive == active) return;
            IsActive = active;
            OnActiveChanged(active);
        }

        internal void Revisit() => OnRevisited();

        protected virtual void OnActiveChanged(bool active) { }

        /// <summary>Ctrl+F: az oldal keresőmezőjére ugrik. Igaz, ha az oldalnak van ilyen.</summary>
        public virtual bool FocusSearch() => false;

        /// <summary>Az oldal a gyorsítótárból került újra elő: a magától frissülő oldalak itt zárkóznak fel.</summary>
        protected virtual void OnRevisited() { }
        private int lastFitWidth = -1;

        /// <summary>Az oldal tartalma elkészült (vagy hibát mutat): a főablak ekkor cseréli le a régi oldalt.</summary>
        public event Action? Ready;

        /// <summary>Az oldal navigációt kér (pl. a főoldali gomb az Oktatók oldalra visz). Paraméter: a menüpont kulcsa.</summary>
        public event Action<string>? NavigateRequested;

        /// <summary>Az oldal kijelentkezést kér (a Profil oldal gombja).</summary>
        public event Action? LogoutRequested;

        protected PageBase(string title, string subtitle, bool scrollable = true)
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.PageBg;
            DoubleBuffered = true;

            // a betöltésjelző csak akkor jelenik meg, ha a betöltés érezhetően tart (egyébként csak villanás lenne)
            loaderDelay = new Debouncer(250, ShowLoader);

            scroll = new SmoothScrollPanel { Dock = DockStyle.Fill, BackColor = Theme.PageBg };
            Body = scroll.Content;
            Body.Padding = Ui.Pad(36, 4, 22, 28);
            Body.ClientSizeChanged += (_, _) => FitAll();

            // a fejléc magassága a szöveg magasságából jön (nem rögzített), így semmilyen nagyításnál nem fedi egymást a cím és az alcím
            var header = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Theme.PageBg,
                Padding = Ui.Pad(33, 18, 0, 12)
            };
            header.Controls.Add(Ui.Lbl(title, 23F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 2)));
            header.Controls.Add(Ui.Lbl(subtitle, 10.5F, Theme.Muted, margin: Ui.Pad(4, 0, 0, 0)));

            Surface.BackColor = Theme.PageBg;

            if (scrollable)
            {
                Controls.Add(scroll);  // a kitöltő (Fill) elem legyen elöl a sorrendben
                Controls.Add(header);
            }
            else
            {
                // a saját elrendezésű oldalak (Chat) fejléc és görgetés nélkül, teljes felületet kapnak
                Controls.Add(Surface);
            }
        }

        /// <summary>
        /// Az oldal újraépítése előtt (témaváltás, másik monitor) elmenti, amit a felhasználó beállított
        /// (keresőszöveg, szűrők, megnyitott beszélgetés), hogy az új oldal onnan folytathassa.
        /// </summary>
        public virtual object? CaptureState() => null;

        /// <summary>Az előző példány CaptureState() értékét kapja meg, még a betöltés (LoadAsync) előtt.</summary>
        public virtual void RestoreState(object? state) { }

        protected void Navigate(string key) => NavigateRequested?.Invoke(key);

        protected void RequestLogout() => LogoutRequested?.Invoke();

        /// <summary>Betölti az oldal tartalmát; hiba esetén hibakártyát mutat újrapróbálás gombbal.</summary>
        public async Task LoadAsync()
        {
            if (loading) return;
            loading = true;
            bool ok = false;

            // a meglévő tartalom a betöltés alatt megmarad (újratöltésnél nem tűnik el), a jelző csak késve jelenik meg
            loaderDelay.Trigger();
            try
            {
                await BuildAsync();
                ok = true;
            }
            catch (ApiException ex)
            {
                if (!IsDisposed) ShowError(ex.Message);
            }
            catch (Exception ex) when (ex is not ObjectDisposedException)
            {
                if (!IsDisposed) ShowError("Váratlan hiba: " + ex.Message);
            }
            finally
            {
                loading = false;
                if (!IsDisposed)
                {
                    Loaded = ok;
                    if (ok)
                    {
                        loadedTick = Environment.TickCount64;
                        staleMarked = false;
                    }
                    loaderDelay.Stop();
                    RemoveLoader();
                    ResumeBodyLayout(); // a sorok egyetlen elrendezéssel jelenjenek meg, mielőtt az oldal láthatóvá válik
                    Ready?.Invoke();
                }
            }
        }

        /// <summary>
        /// Már betöltött oldal újraépítése a helyén: a felhasználó beállításai (szűrők, keresés) és a görgetés megmaradnak.
        /// </summary>
        public async Task ReloadAsync()
        {
            if (loading) return;
            if (!Loaded)
            {
                await LoadAsync();
                return;
            }

            bool keepScroll = !HasPendingRequest;
            RestoreState(CaptureState());
            double offset = scroll.Offset;

            await LoadAsync();
            if (keepScroll && !IsDisposed) scroll.JumpTo(offset);
        }

        private void ShowLoader()
        {
            if (IsDisposed || Body.Controls.Count > 0) return; // van már tartalom: nincs szükség jelzőre

            loaderRow = Ui.Loading();
            AddRow(loaderRow);
        }

        private void RemoveLoader()
        {
            if (loaderRow == null) return;

            if (!loaderRow.IsDisposed)
            {
                Body.Controls.Remove(loaderRow);
                loaderRow.Dispose();
            }
            loaderRow = null;
        }

        /// <summary>Az adatok lekérése után hívd a ClearBody()-t, majd add hozzá a sorokat az AddRow-val.</summary>
        protected abstract Task BuildAsync();

        /// <summary>Új sor a tartalomhoz (a szélességét az oldal igazítja a tartalométhoz).</summary>
        /// <param name="animate">Kompatibilitás miatt marad: a sorok már nem animálva jelennek meg.</param>
        protected void AddRow(Control control, bool animate = true)
        {
            FitWidth(control);
            Body.Controls.Add(control);
        }

        /// <summary>
        /// Kiüríti a tartalmat. Az ezután (ugyanabban a szinkron szakaszban) hozzáadott sorok egyetlen
        /// elrendezéssel jelennek meg, így nincs félig felépült állapot a képernyőn.
        /// </summary>
        protected void ClearBody()
        {
            var controls = Body.Controls.Cast<Control>().ToArray();

            if (!layoutHeld)
            {
                layoutHeld = true;
                Body.SuspendLayout();
                if (IsHandleCreated)
                {
                    Redraw.Suspend(Body); // a kiürített, még üres tartalom ne villanjon fel az újraépítés előtt
                    redrawHeld = true;
                    BeginInvoke(new Action(ResumeBodyLayout));
                }
            }

            Body.Controls.Clear();
            foreach (var control in controls)
                control.Dispose();
            loaderRow = null;
        }

        private void ResumeBodyLayout()
        {
            if (!layoutHeld || IsDisposed) return;

            layoutHeld = false;
            Body.ResumeLayout(true);
            if (redrawHeld)
            {
                redrawHeld = false;
                Redraw.Resume(Body);
            }
        }

        /// <summary>
        /// Az anchor utáni sorok cseréje egyetlen lépésben: közben nincs újrarajzolás és újraelrendezés, ezért a szűrés, fülváltás
        /// vagy lista-frissítés közben nincs villanás (üres vagy félig felépült lista).
        /// </summary>
        protected void ReplaceRowsAfter(Control anchor, Action build)
        {
            Redraw.Suspend(Body);
            Body.SuspendLayout();
            try
            {
                RemoveRowsAfter(anchor);
                build();
            }
            finally
            {
                Body.ResumeLayout(true);
                Redraw.Resume(Body);
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

        protected virtual void ShowError(string message)
        {
            ClearBody();
            var card = new CardPanel();
            card.Controls.Add(Ui.Lbl("Nem sikerült betölteni", 12F, Theme.Danger, bold: true));
            card.Controls.Add(Ui.Lbl(message, 10.5F, Theme.Muted, wrap: true));
            card.Controls.Add(Ui.PrimaryBtn("Újrapróbálás", (_, _) => BeginInvoke(new Action(() => _ = LoadAsync()))));
            AddRow(card);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) loaderDelay.Dispose();
            base.Dispose(disposing);
        }

        private int ContentWidth => Math.Max(Body.ClientSize.Width - Body.Padding.Horizontal, 120);

        private void FitAll()
        {
            // a tartalom magasságának változása (új sor) nem érinti a szélességet
            int width = ContentWidth;
            if (width == lastFitWidth) return;
            lastFitWidth = width;

            // egyben, felfüggesztett elrendezéssel: így a sok sor nem számolja újra az elrendezést soronként
            Body.SuspendLayout();
            foreach (Control control in Body.Controls)
                FitWidth(control);
            Body.ResumeLayout(true);
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

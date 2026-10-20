using System.Globalization;

namespace TutorConnectApp
{
    // ================================================================
    // TANTÁRGYAK: keresés, kategória-szűrés, kategória-színű kártyák oktatószámmal;
    // diáknál a kártyára kattintva az adott tantárgy oktatói nyílnak meg
    // ================================================================
    internal class SubjectsPage : PageBase
    {
        private const string AllCategories = "Minden tárgy";

        // a weboldal kategória-sorrendje; az adatbázisban lévő további kategóriák a végére kerülnek
        private static readonly string[] KnownCategories =
            { "Reál", "Humán", "Nyelvek", "Természettudomány", "Művészet", "Gazdaság", "Egyéb" };

        private readonly bool studentView;
        private readonly Dictionary<string, SelectableChip> categoryChips = new();
        private List<SubjectDto> subjects = new();
        private List<TutorDto> tutors = new();
        private Dictionary<string, int> tutorCounts = new(StringComparer.CurrentCultureIgnoreCase);
        private SearchBox search = null!;
        private Label countLabel = null!;
        private FlowLayoutPanel grid = null!;
        private string selectedCategory = AllCategories;
        private State? restored;
        private int limit = PageSize;
        private Control moreRow = null!;
        private HoverButton moreButton = null!;
        private readonly Debouncer searchDebounce;

        /// <summary>Ennyi kártyát rajzolunk egyszerre; a többi gombnyomásra jelenik meg (sok tantárgynál így nem lassul az oldal).</summary>
        private const int PageSize = 48;

        private sealed record State(string Query, string Category);

        public override object? CaptureState() => search == null ? null : new State(search.Value, selectedCategory);

        public override void RestoreState(object? state) => restored = state as State;

        public SubjectsPage(UserInfo user) : base("Tantárgyak", "Böngéssz a tantárgyak között, majd nézd meg, kik tanítják őket.")
        {
            studentView = user.Role == "STUDENT";

            // a találatok a gépelés szünetében frissülnek, nem minden betűnél
            searchDebounce = new Debouncer(160, () =>
            {
                limit = PageSize;
                RenderGrid();
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) searchDebounce.Dispose();
            base.Dispose(disposing);
        }

        protected override async Task BuildAsync()
        {
            var subjectsTask = ApiClient.GetCachedAsync<List<SubjectDto>>("/api/subjects");
            var tutorsTask = ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
            await Task.WhenAll(subjectsTask, tutorsTask);
            if (IsDisposed) return;

            subjects = subjectsTask.Result;
            tutors = tutorsTask.Result;
            tutorCounts = CountTutorsPerSubject(tutors);
            selectedCategory = AllCategories;
            categoryChips.Clear();
            limit = PageSize;

            ClearBody();

            // eszköztár: keresés + kategória-szűrők egy kártyában
            var toolbar = new CardPanel();
            search = new SearchBox("Keresés tantárgy neve alapján...") { Tag = "fill" };
            if (restored != null) search.SetValue(restored.Query); // még a figyelő bekötése előtt, hogy ne rajzoljon
            search.ValueChanged += (_, _) => searchDebounce.Trigger();
            toolbar.Controls.Add(search);

            var filters = Ui.WrapRow();
            var present = subjects.Select(s => s.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
            var categories = new List<string> { AllCategories };
            categories.AddRange(KnownCategories.Where(present.Contains));
            categories.AddRange(present.Where(c => !KnownCategories.Contains(c)));

            foreach (string category in categories)
            {
                string value = category;
                int count = category == AllCategories ? subjects.Count : subjects.Count(s => s.Category == category);
                var chip = new SelectableChip($"{category} ({count})");
                chip.Click += (_, _) => SelectCategory(value);
                categoryChips[category] = chip;
                filters.Controls.Add(chip);
            }
            filters.Controls.Add(Ui.Btn("Szűrők törlése", Theme.CardBg, Theme.Muted, (_, _) => ClearFilters(), outline: true));
            toolbar.Controls.Add(filters);
            AddRow(toolbar);

            countLabel = Ui.Lbl("", 10F, Theme.Muted, margin: Ui.Pad(2, 0, 0, 12));
            AddRow(countLabel);

            grid = Ui.WrapRow();
            AddRow(grid);

            var more = Ui.Row();
            moreButton = Ui.Btn("Továbbiak megjelenítése", Theme.CardBg, Theme.Text, (_, _) =>
            {
                limit += PageSize;
                RenderGrid();
            }, outline: true);
            more.Controls.Add(moreButton);
            moreRow = more;
            AddRow(moreRow, animate: false);

            if (restored != null && categoryChips.ContainsKey(restored.Category))
                selectedCategory = restored.Category;
            restored = null;

            categoryChips[selectedCategory].SetSelected(true);
            RenderGrid();
        }

        private void SelectCategory(string category)
        {
            limit = PageSize;
            selectedCategory = category;
            foreach (var (name, chip) in categoryChips)
                chip.SetSelected(name == category);
            RenderGrid();
        }

        private void ClearFilters()
        {
            search.SetValue(""); // a ValueChanged újrarajzolja a listát
            SelectCategory(AllCategories);
        }

        /// <summary>Hány oktató tanítja az adott tantárgyat (a számok betöltéskor egyszer készülnek el, nem kártyánként).</summary>
        private int TutorCountFor(string subjectName) =>
            tutorCounts.TryGetValue(subjectName.Trim(), out int count) ? count : 0;

        private static Dictionary<string, int> CountTutorsPerSubject(IEnumerable<TutorDto> tutors)
        {
            var counts = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var tutor in tutors)
            {
                var names = (tutor.Subjects ?? "")
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Distinct(StringComparer.CurrentCultureIgnoreCase);

                foreach (string name in names)
                    counts[name] = counts.GetValueOrDefault(name) + 1;
            }
            return counts;
        }

        private void RenderGrid()
        {
            string query = search.Value.Trim();
            var shown = subjects
                .Where(s => selectedCategory == AllCategories
                            || string.Equals(s.Category, selectedCategory, StringComparison.CurrentCultureIgnoreCase))
                .Where(s => query.Length == 0 || s.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            countLabel.Text = shown.Count switch
            {
                0 => "Nincs találat",
                1 => "1 tantárgy",
                _ => $"{shown.Count} tantárgy"
            };

            int remaining = Math.Max(shown.Count - limit, 0);
            moreRow.Visible = remaining > 0;
            moreButton.Text = $"Továbbiak megjelenítése ({remaining})";

            grid.SuspendLayout();
            Ui.Reset(grid);

            if (shown.Count == 0)
            {
                grid.Controls.Add(Ui.MessageCard("Nem található a keresésnek és a szűrőknek megfelelő tantárgy.", "\uE721"));
            }
            else
            {
                foreach (var subject in shown.Take(limit))
                {
                    var card = new SubjectCard(subject, TutorCountFor(subject.Name), studentView);
                    if (studentView)
                        card.Click += (_, _) => OpenTutors(subject.Name);
                    grid.Controls.Add(card);
                }
            }

            grid.ResumeLayout(true);
        }

        private void OpenTutors(string subjectName)
        {
            TutorsPage.InitialSubject = subjectName;
            Navigate("tutors");
        }
    }
}

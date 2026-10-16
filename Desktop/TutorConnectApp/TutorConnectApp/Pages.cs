using System.Globalization;

namespace TutorConnectApp
{
    // ================================================================
    // FŐOLDAL: üdvözlő sáv, saját foglalások, következő órák, oldal-statisztikák
    // ================================================================
    internal class HomePage : PageBase
    {
        private readonly UserInfo user;

        public HomePage(UserInfo user) : base("Főoldal", "Minden fontos egy helyen.")
        {
            this.user = user;
        }

        protected override async Task BuildAsync()
        {
            var statsTask = ApiClient.GetAsync<StatsDto>("/api/stats");
            var summaryTask = ApiClient.GetAsync<BookingSummaryDto>("/api/bookings/summary");
            var upcomingTask = ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=upcoming");
            await Task.WhenAll(statsTask, summaryTask, upcomingTask);
            if (IsDisposed) return;

            var stats = statsTask.Result;
            var summary = summaryTask.Result;
            var upcoming = upcomingTask.Result;
            bool tutor = user.Role == "TUTOR";
            bool student = user.Role == "STUDENT";

            ClearBody();

            string? buttonText = student ? "Új foglalás" : tutor ? "Időpontjaim kezelése" : null;
            AddRow(new HeroBanner($"Szia, {Ui.FirstName(user.Name)}!", Ui.RoleIntro(user.Role), buttonText, () =>
            {
                if (student)
                {
                    Navigate("tutors");
                }
                else if (tutor)
                {
                    BookingsPage.RequestedTab = "slots";
                    Navigate("bookings");
                }
            }));

            AddRow(Ui.Section("Foglalásaid"));
            var mine = Ui.WrapRow();
            mine.Controls.Add(new StatTile(summary.Pending, "", tutor ? "Jóváhagyásra vár" : "Függőben", "", Ui.Warning));
            mine.Controls.Add(new StatTile(summary.Upcoming, "", "Közelgő óra", "", Ui.Success));
            mine.Controls.Add(new StatTile(summary.Past, "", "Lezajlott óra", "", Theme.MainBlue));
            mine.Controls.Add(new StatTile(summary.Cancelled, "", "Lemondott", "", Ui.Muted));
            AddRow(mine);

            AddRow(Ui.Section("Következő óráid"));
            if (upcoming.Count == 0)
            {
                AddRow(Ui.MessageCard(
                    student ? "Nincs közelgő órád. Foglalj időpontot az Oktatók menüpontban." : "Nincs közelgő órád.",
                    ""));
            }
            else
            {
                foreach (var booking in upcoming.Take(3))
                    AddRow(UpcomingCard(booking));
            }

            AddRow(Ui.Section("A TutorConnect számokban"));
            var site = Ui.WrapRow();
            site.Controls.Add(new StatTile(stats.Tutors, "", "Oktató", "", Theme.MainBlueDark));
            site.Controls.Add(new StatTile(stats.Bookings, "", "Foglalás", "", Theme.MainBlueDark));
            site.Controls.Add(new StatTile(stats.Satisfaction, "%", "Elégedettség", "", Theme.MainBlueDark));
            AddRow(site);
        }

        private static CardPanel UpcomingCard(BookingDto b)
        {
            var card = new CardPanel();
            var head = Ui.Row();
            head.Controls.Add(Ui.Lbl(b.SubjectName ?? "Tantárgy", 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 2, 12, 0)));
            head.Controls.Add(Ui.Chip("Visszaigazolva", Ui.Success));
            card.Controls.Add(head);
            card.Controls.Add(Ui.Lbl((b.AsStudent ? "Oktató: " : "Diák: ") + (b.OtherName ?? "ismeretlen"), 10.5F, Theme.Text));
            card.Controls.Add(Ui.Lbl(Ui.FormatRange(b.StartTime, b.EndTime), 10F, Theme.Muted, margin: Ui.Pad(0)));
            return card;
        }
    }

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
        private SearchBox search = null!;
        private Label countLabel = null!;
        private FlowLayoutPanel grid = null!;
        private string selectedCategory = AllCategories;
        private State? restored;
        private int limit = PageSize;
        private Control moreRow = null!;
        private HoverButton moreButton = null!;

        /// <summary>Ennyi kártyát rajzolunk egyszerre; a többi gombnyomásra jelenik meg (sok tantárgynál így nem lassul az oldal).</summary>
        private const int PageSize = 48;

        private sealed record State(string Query, string Category);

        public override object? CaptureState() => search == null ? null : new State(search.Value, selectedCategory);

        public override void RestoreState(object? state) => restored = state as State;

        public SubjectsPage(UserInfo user) : base("Tantárgyak", "Böngéssz a tantárgyak között, majd nézd meg, kik tanítják őket.")
        {
            studentView = user.Role == "STUDENT";
        }

        protected override async Task BuildAsync()
        {
            var subjectsTask = ApiClient.GetCachedAsync<List<SubjectDto>>("/api/subjects");
            var tutorsTask = ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
            await Task.WhenAll(subjectsTask, tutorsTask);
            if (IsDisposed) return;

            subjects = subjectsTask.Result;
            tutors = tutorsTask.Result;
            selectedCategory = AllCategories;
            categoryChips.Clear();
            limit = PageSize;

            ClearBody();

            // eszköztár: keresés + kategória-szűrők egy kártyában
            var toolbar = new CardPanel();
            search = new SearchBox("Keresés tantárgy neve alapján...") { Tag = "fill" };
            if (restored != null) search.SetValue(restored.Query); // még a figyelő bekötése előtt, hogy ne rajzoljon
            search.ValueChanged += (_, _) =>
            {
                limit = PageSize;
                RenderGrid();
            };
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

        private int TutorCountFor(string subjectName) =>
            tutors.Count(t => (t.Subjects ?? "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(s => string.Equals(s, subjectName.Trim(), StringComparison.CurrentCultureIgnoreCase)));

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

    // ================================================================
    // OKTATÓK (diák): keresés, tantárgy / értékelés szűrő, rendezés + időpontfoglalás
    // ================================================================
    internal class TutorsPage : PageBase
    {
        /// <summary>A Tantárgyak oldal ezzel nyitja meg az oktatókat egy adott tantárgyra szűrve.</summary>
        public static string? InitialSubject;

        private const string AllSubjects = "Összes tantárgy";

        private static readonly (string Label, decimal Min)[] RatingOptions =
        {
            ("Bármilyen", 0m), ("4.0 felett", 4.0m), ("4.5 felett", 4.5m), ("4.8 felett", 4.8m)
        };

        private static readonly string[] SortOptions =
            { "Alapértelmezett", "Legjobb értékelés", "Ár szerint növekvő", "Ár szerint csökkenő" };

        private List<TutorDto> tutors = new();
        private SearchBox search = null!;
        private DropdownSelect subjectBox = null!;
        private DropdownSelect ratingBox = null!;
        private DropdownSelect sortBox = null!;
        private DropdownSelect minPriceBox = null!;
        private DropdownSelect maxPriceBox = null!;
        private DropdownSelect viewBox = null!;
        private Label countLabel = null!;
        private State? restored;
        private int limit = PageSize;

        /// <summary>Ennyi oktatókártyát rajzolunk egyszerre; a többi gombnyomásra jelenik meg.</summary>
        private const int PageSize = 12;

        /// <summary>Az óradíj-szűrők választható értékei (0 = bármennyi).</summary>
        private static readonly int[] PriceSteps = { 0, 3000, 4000, 5000, 6000, 8000, 10000, 15000 };

        private sealed record State(string Query, int Subject, int Rating, int Sort, int MinPrice, int MaxPrice, int View);

        public override object? CaptureState() =>
            search == null
                ? null
                : new State(search.Value, subjectBox.SelectedIndex, ratingBox.SelectedIndex, sortBox.SelectedIndex,
                    minPriceBox.SelectedIndex, maxPriceBox.SelectedIndex, viewBox.SelectedIndex);

        public override void RestoreState(object? state) => restored = state as State;

        public TutorsPage() : base("Oktatók", "Válaszd ki a hozzád illő oktatót, és foglalj időpontot.") { }

        protected override async Task BuildAsync()
        {
            var tutorsTask = ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
            var subjectsTask = ApiClient.GetCachedAsync<List<SubjectDto>>("/api/subjects");
            await Task.WhenAll(tutorsTask, subjectsTask);
            if (IsDisposed) return;

            tutors = tutorsTask.Result;
            var subjectNames = subjectsTask.Result
                .Select(s => s.Name)
                .Distinct()
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            // a Tantárgyak oldalról érkezve az adott tantárgy már ki van választva
            int initial = InitialSubject == null
                ? 0
                : subjectNames.FindIndex(n => string.Equals(n, InitialSubject, StringComparison.CurrentCultureIgnoreCase)) + 1;
            bool fromSubjects = InitialSubject != null;
            InitialSubject = null;

            // témaváltás után a korábbi keresés és szűrők visszaállnak (a Tantárgyak oldalról érkezve nem)
            var keep = fromSubjects ? null : restored;
            restored = null;
            if (keep != null) initial = keep.Subject;
            limit = PageSize;

            ClearBody();

            search = new SearchBox("Keresés oktató neve vagy tantárgy alapján...");
            if (keep != null) search.SetValue(keep.Query); // még a figyelő bekötése előtt, hogy ne rajzoljon
            search.ValueChanged += (_, _) =>
            {
                limit = PageSize;
                RenderList(animate: false);
            };
            AddRow(search);

            subjectBox = new DropdownSelect("Tantárgy", new[] { AllSubjects }.Concat(subjectNames), initial);
            ratingBox = new DropdownSelect("Értékelés", RatingOptions.Select(o => o.Label), keep?.Rating ?? 0);
            sortBox = new DropdownSelect("Rendezés", SortOptions, keep?.Sort ?? 0);
            minPriceBox = new DropdownSelect("Óradíj legalább", PriceSteps.Select(PriceLabel), keep?.MinPrice ?? 0);
            maxPriceBox = new DropdownSelect("Óradíj legfeljebb", PriceSteps.Select(PriceLabel), keep?.MaxPrice ?? 0);
            viewBox = new DropdownSelect("Megjelenítés", new[] { "Minden oktató", "Csak a kedvenceim" }, keep?.View ?? 0);
            foreach (var box in new[] { subjectBox, ratingBox, sortBox, minPriceBox, maxPriceBox, viewBox })
                box.SelectionChanged += (_, _) =>
                {
                    limit = PageSize;
                    RenderList(animate: false);
                };

            var options = Ui.WrapRow();
            options.Controls.Add(subjectBox);
            options.Controls.Add(ratingBox);
            options.Controls.Add(sortBox);
            options.Controls.Add(minPriceBox);
            options.Controls.Add(maxPriceBox);
            options.Controls.Add(viewBox);
            AddRow(options);

            countLabel = Ui.Lbl("", 10F, Theme.Muted, margin: Ui.Pad(2, 0, 0, 12));
            AddRow(countLabel);

            RenderList(animate: true);
        }

        private static string PriceLabel(int value) => value == 0 ? "Bármennyi" : Ui.Money(value);

        /// <summary>Az oktató legkisebb és legnagyobb óradíja; (0, 0), ha nincs megadva.</summary>
        private static (decimal Min, decimal Max) PriceBounds(TutorDto tutor)
        {
            var prices = ParseSubjects(tutor.SubjectPrices).Select(s => s.Price).Where(p => p > 0).ToList();
            if (prices.Count == 0 && tutor.HourlyRate > 0) prices.Add(tutor.HourlyRate);
            return prices.Count == 0 ? (0m, 0m) : (prices.Min(), prices.Max());
        }

        private static bool HasSubject(TutorDto tutor, string subject) =>
            (tutor.Subjects ?? "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(s => string.Equals(s, subject, StringComparison.CurrentCultureIgnoreCase));

        private void RenderList(bool animate)
        {
            string query = search.Value.Trim();
            string? subject = subjectBox.SelectedIndex > 0 ? subjectBox.SelectedText : null;
            decimal minRating = RatingOptions[ratingBox.SelectedIndex].Min;
            int minPrice = PriceSteps[minPriceBox.SelectedIndex];
            int maxPrice = PriceSteps[maxPriceBox.SelectedIndex];
            bool onlyFavorites = viewBox.SelectedIndex == 1;

            IEnumerable<TutorDto> filtered = tutors.Where(t =>
                (query.Length == 0
                    || t.FullName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || (t.Subjects ?? "").Contains(query, StringComparison.CurrentCultureIgnoreCase))
                && (subject == null || HasSubject(t, subject))
                && t.AverageRating >= minRating
                && (!onlyFavorites || Favorites.IsFavorite(t.Id))
                && MatchesPrice(t, minPrice, maxPrice));

            filtered = sortBox.SelectedIndex switch
            {
                1 => filtered.OrderByDescending(t => t.AverageRating).ThenByDescending(t => t.ReviewCount),
                // az óradíj nélküli oktatók a végére kerülnek
                2 => filtered.OrderBy(t => t.HourlyRate > 0 ? t.HourlyRate : decimal.MaxValue),
                3 => filtered.OrderByDescending(t => t.HourlyRate),
                _ => filtered
            };
            var shown = filtered.ToList();

            countLabel.Text = shown.Count switch
            {
                0 => "Nincs találat",
                1 => "1 oktató",
                _ => $"{shown.Count} oktató"
            };

            Body.SuspendLayout();
            RemoveRowsAfter(countLabel);

            if (shown.Count == 0)
                AddRow(Ui.MessageCard("Nincs a szűrőknek megfelelő oktató.", "\uE721"), animate);
            else
            {
                foreach (var tutor in shown.Take(limit))
                    AddRow(BuildCard(tutor), animate);

                if (shown.Count > limit)
                {
                    var more = Ui.Row();
                    more.Controls.Add(Ui.Btn($"Továbbiak megjelenítése ({shown.Count - limit})", Theme.CardBg, Theme.Text, (_, _) =>
                    {
                        // a gomb a saját eltávolítását váltja ki, ezért a kattintás után hajtjuk végre
                        BeginInvoke(new Action(() =>
                        {
                            limit += PageSize;
                            RenderList(animate: false);
                        }));
                    }, outline: true));
                    AddRow(more, animate: false);
                }
            }

            Body.ResumeLayout(true);
        }

        /// <summary>Az óradíj-tartomány átfedi a szűrő sávját? Óradíj nélküli oktató csak akkor jelenik meg, ha nincs ár-szűrő.</summary>
        private static bool MatchesPrice(TutorDto tutor, int minPrice, int maxPrice)
        {
            if (minPrice == 0 && maxPrice == 0) return true;

            var (low, high) = PriceBounds(tutor);
            if (high == 0) return false;
            return (minPrice == 0 || high >= minPrice) && (maxPrice == 0 || low <= maxPrice);
        }

        private CardPanel BuildCard(TutorDto tutor)
        {
            // a kiemelkedően jól értékelt oktatók kártyáján arany jelzősáv van
            bool top = tutor.ReviewCount >= 3 && tutor.AverageRating >= 4.5m;
            var card = new AccentCard(top ? Theme.Star : Theme.MainBlue);

            // ---------- fej: avatar, név, értékelés, óradíj ----------
            var head = Ui.Row();
            head.Controls.Add(new AvatarCircle(tutor.FullName, 64));

            var info = Ui.Row();
            info.FlowDirection = FlowDirection.TopDown;
            info.Controls.Add(Ui.Lbl(tutor.FullName, 14.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 2, 0, 2)));

            var rating = Ui.Row();
            if (tutor.ReviewCount > 0)
            {
                rating.Controls.Add(Ui.Lbl(Stars(tutor.AverageRating), 11.5F, Theme.Star, margin: Ui.Pad(0, 0, 8, 0)));
                rating.Controls.Add(Ui.Lbl($"{tutor.AverageRating.ToString("0.0", Ui.Hu)}  ({tutor.ReviewCount} értékelés)", 10F, Theme.Muted, margin: Ui.Pad(0, 3, 0, 0)));
            }
            else
            {
                rating.Controls.Add(Ui.Lbl("Még nincs értékelés", 10F, Theme.Muted, margin: Ui.Pad(0, 1, 0, 0)));
            }
            info.Controls.Add(rating);

            string range = PriceRange(tutor);
            if (range.Length > 0)
                info.Controls.Add(Ui.Lbl(range, 10.5F, Theme.MainBlue, bold: true, margin: Ui.Pad(0, 2, 0, 0)));
            head.Controls.Add(info);
            card.Controls.Add(head);

            // ---------- tantárgyak ----------
            var chips = Ui.WrapRow();
            foreach (var (name, price) in ParseSubjects(tutor.SubjectPrices))
                chips.Controls.Add(Ui.Chip(price > 0 ? $"{name} · {Ui.Money(price)}/óra" : name));
            card.Controls.Add(chips);

            // ---------- bemutatkozás (rövidítve; a teljes a profilon) ----------
            if (!string.IsNullOrWhiteSpace(tutor.Bio))
            {
                string bio = tutor.Bio!.Trim();
                if (bio.Length > 220) bio = bio[..220].TrimEnd() + "…";
                card.Controls.Add(Ui.Lbl(bio, 10F, Theme.Muted, wrap: true, margin: Ui.Pad(0, 4, 0, 8)));
            }

            // ---------- műveletek ----------
            var actions = Ui.Row();
            actions.Controls.Add(Ui.Btn("Profil megtekintése", Theme.CardBg, Theme.MainBlue, async (_, _) => await OpenProfileAsync(tutor), outline: true));
            actions.Controls.Add(Ui.PrimaryBtn("Időpont foglalása", (_, _) => OpenBooking(tutor)));
            actions.Controls.Add(Ui.Btn("Naptár", Theme.CardBg, Theme.Text, (_, _) => OpenCalendar(tutor), outline: true));
            actions.Controls.Add(Ui.Btn("Üzenet", Theme.CardBg, Theme.Text, async (_, _) => await MessageAsync(tutor), outline: true));

            // kedvenc: a szív megnyomására elmentődik; ha az oktató új időpontot hirdet, az app értesít
            HoverButton? favorite = null;
            favorite = Ui.Btn(Favorites.IsFavorite(tutor.Id) ? "♥ Kedvenc" : "♡ Kedvenc", Theme.CardBg,
                Favorites.IsFavorite(tutor.Id) ? Theme.Danger : Theme.Text, (_, _) =>
                {
                    bool now = Favorites.Toggle(tutor.Id);
                    favorite!.Text = now ? "♥ Kedvenc" : "♡ Kedvenc";
                    favorite.ForeColor = now ? Theme.Danger : Theme.Text;
                    if (viewBox.SelectedIndex == 1 && !now)
                        BeginInvoke(new Action(() => RenderList(animate: false))); // a kedvencek listájából kikerül
                }, outline: true);
            actions.Controls.Add(favorite);
            card.Controls.Add(actions);
            return card;
        }

        private static string Stars(decimal average)
        {
            int full = Math.Clamp((int)Math.Round(average, MidpointRounding.AwayFromZero), 0, 5);
            return new string('★', full) + new string('☆', 5 - full);
        }

        /// <summary>Az oktató óradíj-tartománya szövegként ("6 500 – 9 000 Ft/óra"); üres, ha nincs megadva.</summary>
        internal static string PriceRange(TutorDto tutor)
        {
            var prices = ParseSubjects(tutor.SubjectPrices).Select(s => s.Price).Where(p => p > 0).ToList();
            if (prices.Count == 0) return tutor.HourlyRate > 0 ? $"{Ui.Money(tutor.HourlyRate)}/óra" : "";

            decimal min = prices.Min();
            decimal max = prices.Max();
            return min == max
                ? $"{Ui.Money(min)}/óra"
                : $"{min.ToString("N0", Ui.Hu)} – {Ui.Money(max)}/óra";
        }

        private void OpenCalendar(TutorDto tutor)
        {
            CalendarPage.InitialTutorId = tutor.Id;
            Navigate("calendar");
        }

        /// <summary>Megnyitja az oktató teljes profilját; onnan foglalás, naptár vagy üzenet indítható.</summary>
        private Task OpenProfileAsync(TutorDto tutor) => TutorActions.ShowProfileAsync(FindForm(), tutor, Navigate);

        /// <summary>Beszélgetést indít az oktatóval (vagy megnyitja a meglévőt), és átvált a Chatre.</summary>
        private async Task MessageAsync(TutorDto tutor)
        {
            try
            {
                var conversation = await ApiClient.PostAsync<ConversationDto>("/api/conversations",
                    new Dictionary<string, object?> { ["tutor_id"] = tutor.Id });
                if (IsDisposed) return;

                ChatPage.PendingConversationId = conversation.Id;
                Navigate("chat");
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Nem sikerült megnyitni a beszélgetést", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenBooking(TutorDto tutor)
        {
            using var dialog = new BookDialog(tutor);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                MessageBox.Show(FindForm(),
                    "A foglalási kérelmet elküldtük. Az oktató visszaigazolására vár.\nA Foglalás menüpontban követheted.",
                    "Foglalás elküldve", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Navigate("bookings");
            }
        }

        internal static IEnumerable<(string Name, decimal Price)> ParseSubjects(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) yield break;

            foreach (var entry in raw.Split(";;", StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('|');
                if (parts.Length < 2) continue;
                decimal price = 0;
                if (parts.Length >= 3)
                    decimal.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out price);
                yield return (parts[1], price);
            }
        }
    }

    // ================================================================
    // FOGLALÁSOK (diák és oktató): állapot szerinti fülek, műveletek, értékelés;
    // oktatónak: saját szabad időpontok kezelése
    // ================================================================
    internal class BookingsPage : PageBase
    {
        /// <summary>A főoldali gomb ezzel nyit meg egy adott fület (pl. "slots").</summary>
        public static string? RequestedTab;

        private readonly UserInfo user;
        private readonly (string Key, string Label)[] tabs;
        private readonly Dictionary<string, SelectableChip> tabChips = new();
        private BookingSummaryDto summary = new();
        private FlowLayoutPanel tabRow = null!;
        private readonly Dictionary<string, CountCard> countCards = new();
        private string filter = "";
        private int loadId;

        public BookingsPage(UserInfo user) : base(
            user.Role == "TUTOR" ? "Foglalások" : "Foglalásaim",
            user.Role == "TUTOR" ? "Fogadd el a kérelmeket, és add meg, mikor érsz rá." : "Kövesd nyomon az óráidat, és foglalj újat.")
        {
            this.user = user;

            var list = new List<(string, string)>
            {
                ("pending", "Függőben"),
                ("upcoming", "Közelgő"),
                ("past", "Korábbi"),
                ("cancelled", "Lemondott")
            };
            if (user.Role == "TUTOR")
                list.Add(("slots", "Időpontjaim"));
            tabs = list.ToArray();
        }

        protected override async Task BuildAsync()
        {
            summary = await ApiClient.GetAsync<BookingSummaryDto>("/api/bookings/summary");
            if (IsDisposed) return;

            if (filter.Length == 0)
            {
                if (RequestedTab != null && tabs.Any(t => t.Key == RequestedTab))
                    filter = RequestedTab;
                else
                    filter = user.Role == "TUTOR" && summary.Pending > 0 ? "pending" : "upcoming";
            }
            RequestedTab = null;

            ClearBody();
            tabChips.Clear();
            countCards.Clear();

            if (user.Role == "STUDENT")
            {
                var top = Ui.Row();
                top.Margin = Ui.Pad(0, 0, 0, 12);
                top.Controls.Add(Ui.PrimaryBtn("+  Új foglalás", (_, _) => Navigate("tutors")));
                AddRow(top);
            }

            // áttekintő: számlálókártyák, amelyekre kattintva az adott lista nyílik meg
            var overview = Ui.WrapRow();
            foreach (var (key, title, glyph, color) in new[]
            {
                ("pending", "Függőben", "\uE121", Theme.Warning),
                ("upcoming", "Közelgő", "\uE787", Theme.Success),
                ("past", "Korábbi", "\uE81C", Theme.MainBlue),
                ("cancelled", "Lemondott", "\uE711", Theme.Muted)
            })
            {
                string cardKey = key;
                var card = new CountCard(title, glyph, color, async () =>
                {
                    if (filter == cardKey) return;
                    filter = cardKey;
                    await LoadListAsync();
                });
                countCards[key] = card;
                overview.Controls.Add(card);
            }
            AddRow(overview);

            tabRow = Ui.WrapRow();
            foreach (var (key, _) in tabs)
            {
                string tabKey = key;
                var chip = new SelectableChip(key);
                chip.Click += async (_, _) =>
                {
                    if (filter == tabKey) return;
                    filter = tabKey;
                    await LoadListAsync();
                };
                tabChips[key] = chip;
                tabRow.Controls.Add(chip);
            }
            AddRow(tabRow);

            await LoadListAsync();
        }

        private int? CountFor(string key) => key switch
        {
            "pending" => summary.Pending,
            "upcoming" => summary.Upcoming,
            "past" => summary.Past,
            "cancelled" => summary.Cancelled,
            _ => null
        };

        private void UpdateTabs()
        {
            foreach (var (key, label) in tabs)
            {
                var chip = tabChips[key];
                int? count = CountFor(key);
                chip.SetText(count.HasValue ? $"{label} ({count})" : label);
                chip.SetSelected(key == filter);

                if (countCards.TryGetValue(key, out var card))
                {
                    card.SetCount(count ?? 0);
                    card.SetSelected(key == filter);
                }
            }
        }

        private async Task LoadListAsync()
        {
            int myLoad = ++loadId;
            UpdateTabs();
            RemoveRowsAfter(tabRow);
            AddRow(Ui.Loading(), animate: false);

            try
            {
                if (filter == "slots")
                {
                    var slots = await ApiClient.GetAsync<List<SlotItemDto>>("/api/availability");
                    if (IsDisposed || myLoad != loadId) return;
                    ShowSlots(slots);
                }
                else
                {
                    var bookings = await ApiClient.GetAsync<List<BookingDto>>($"/api/bookings?status={filter}");
                    if (IsDisposed || myLoad != loadId) return;
                    ShowBookings(bookings);
                }
            }
            catch (ApiException ex)
            {
                if (IsDisposed || myLoad != loadId) return;
                RemoveRowsAfter(tabRow);
                AddRow(Ui.MessageCard(ex.Message, ""));
            }
        }

        // ------------------------------------------------------------
        // Foglalások listája
        // ------------------------------------------------------------

        private void ShowBookings(List<BookingDto> bookings)
        {
            Body.SuspendLayout();
            RemoveRowsAfter(tabRow);

            if (bookings.Count == 0)
                AddRow(Ui.MessageCard("Ebben a kategóriában nincs foglalás.", ""));
            else
                foreach (var booking in bookings)
                    AddRow(BuildBookingCard(booking));

            Body.ResumeLayout(true);
        }

        private CardPanel BuildBookingCard(BookingDto b)
        {
            var card = new CardPanel();

            var head = Ui.Row();
            head.Controls.Add(Ui.Lbl(b.SubjectName ?? "Tantárgy", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 2, 12, 0)));
            head.Controls.Add(StatusChip(b.Status));
            card.Controls.Add(head);

            card.Controls.Add(Ui.Lbl((b.AsStudent ? "Oktató: " : "Diák: ") + (b.OtherName ?? "ismeretlen"), 10.5F, Theme.Text));

            string when = Ui.FormatRange(b.StartTime, b.EndTime);
            if (b.Price is > 0) when += $"  ·  {Ui.Money(b.Price.Value)}";
            card.Controls.Add(Ui.Lbl(when, 10F, Theme.Muted));

            if (!string.IsNullOrWhiteSpace(b.Note))
                card.Controls.Add(Ui.Lbl("Üzenet: " + b.Note, 10F, Theme.Muted, wrap: true));

            if (b.Review != null)
            {
                int stars = Math.Clamp(b.Review.Rating, 0, 5);
                string text = "Értékelés: " + new string('★', stars) + new string('☆', 5 - stars);
                if (!string.IsNullOrWhiteSpace(b.Review.Comment)) text += "\n" + b.Review.Comment;
                card.Controls.Add(Ui.Lbl(text, 10F, Theme.Muted, wrap: true));
            }

            bool canReview = b.AsStudent && b.Status == "COMPLETED" && b.Review == null;
            if (b.CanConfirm || b.CanReject || b.CanCancel || canReview)
            {
                var actions = Ui.Row();
                if (b.CanConfirm)
                    actions.Controls.Add(Ui.Btn("Elfogadás", Ui.Success, Theme.White, async (_, _) => await ActAsync(b, "confirm", null)));
                if (b.CanReject)
                    actions.Controls.Add(Ui.Btn("Elutasítás", Theme.CardBg, Ui.Danger,
                        async (_, _) => await ActAsync(b, "reject", "Biztosan elutasítod ezt a foglalást?"), outline: true));
                if (b.CanCancel && !b.CanReject)
                    actions.Controls.Add(Ui.Btn("Lemondás", Theme.CardBg, Ui.Danger,
                        async (_, _) => await ActAsync(b, "cancel", "Biztosan lemondod ezt a foglalást?"), outline: true));
                if (canReview)
                    actions.Controls.Add(Ui.PrimaryBtn("Értékelés írása", async (_, _) => await ReviewAsync(b)));
                card.Controls.Add(actions);
            }

            if (b.AsStudent && b.OtherId > 0)
            {
                var more = Ui.Row();
                more.Controls.Add(Ui.Btn("Oktató profilja", Theme.CardBg, Theme.MainBlue, async (_, _) => await OpenTutorProfileAsync(b.OtherId), outline: true));
                card.Controls.Add(more);
            }

            return card;
        }

        /// <summary>A foglalás oktatójának profilja (a már letöltött oktatólistából).</summary>
        private async Task OpenTutorProfileAsync(int tutorId)
        {
            try
            {
                var tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                if (IsDisposed) return;

                var tutor = tutors.FirstOrDefault(t => t.Id == tutorId);
                if (tutor == null)
                {
                    MessageBox.Show(FindForm(), "Az oktató profilja nem érhető el.", "Oktató profilja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                await TutorActions.ShowProfileAsync(FindForm(), tutor, Navigate);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Nem sikerült megnyitni a profilt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static Label StatusChip(string status) => status switch
        {
            "PENDING" => Ui.Chip("Függőben", Ui.Warning),
            "CONFIRMED" => Ui.Chip("Visszaigazolva", Ui.Success),
            "COMPLETED" => Ui.Chip("Lezajlott", Theme.MainBlue),
            "REJECTED" => Ui.Chip("Elutasítva", Ui.Danger),
            "CANCELLED" => Ui.Chip("Lemondva", Ui.Muted),
            _ => Ui.Chip(status, Ui.Muted)
        };

        private async Task ActAsync(BookingDto booking, string action, string? confirmText)
        {
            if (confirmText != null
                && MessageBox.Show(FindForm(), confirmText, "Megerősítés", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                await ApiClient.PostAsync($"/api/bookings/{booking.Id}/{action}");
                AppEvents.RaiseBadgesStale();
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "A művelet nem sikerült", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (!IsDisposed) await LoadAsync(); // összesítő és lista frissítése
        }

        private async Task ReviewAsync(BookingDto booking)
        {
            using var dialog = new ReviewDialog(booking);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK && !IsDisposed)
                await LoadAsync();
        }

        // ------------------------------------------------------------
        // Oktató: saját szabad időpontok
        // ------------------------------------------------------------

        private void ShowSlots(List<SlotItemDto> slots)
        {
            Body.SuspendLayout();
            RemoveRowsAfter(tabRow);

            var top = Ui.Row();
            top.Margin = Ui.Pad(0, 0, 0, 12);
            top.Controls.Add(Ui.PrimaryBtn("+  Új időpont", async (_, _) => await AddSlotAsync()));
            AddRow(top);

            if (slots.Count == 0)
            {
                AddRow(Ui.MessageCard("Még nem adtál meg szabad időpontot. A diákok csak ezek közül foglalhatnak.", ""));
            }
            else
            {
                foreach (var day in slots.OrderBy(s => s.StartTime).GroupBy(s => s.StartTime.ToLocalTime().Date))
                {
                    var card = new CardPanel();
                    card.Controls.Add(Ui.Lbl(Ui.FormatDay(day.First().StartTime), 11.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 8)));

                    foreach (var slot in day)
                    {
                        var row = Ui.Row();
                        row.Controls.Add(Ui.Lbl(Ui.FormatTimeRange(slot.StartTime, slot.EndTime), 10.5F, Theme.Text, margin: Ui.Pad(0, 9, 16, 0)));
                        row.Controls.Add(slot.IsBooked
                            ? Ui.Chip("Lefoglalva", Ui.Warning)
                            : Ui.Chip("Szabad", Ui.Success));
                        if (!slot.IsBooked)
                        {
                            var delete = Ui.Btn("Törlés", Theme.CardBg, Ui.Danger, async (_, _) => await DeleteSlotAsync(slot), outline: true);
                            delete.Margin = Ui.Pad(8, 0, 0, 0);
                            row.Controls.Add(delete);
                        }
                        card.Controls.Add(row);
                    }

                    AddRow(card);
                }
            }

            Body.ResumeLayout(true);
        }

        private async Task AddSlotAsync()
        {
            using var dialog = new AvailabilityDialog();
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

            MessageBox.Show(FindForm(), dialog.ResultMessage, "Időpont mentve", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadListAsync();
        }

        private async Task DeleteSlotAsync(SlotItemDto slot)
        {
            if (MessageBox.Show(FindForm(), "Biztosan törlöd ezt az időpontot?", "Megerősítés", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                await ApiClient.DeleteAsync($"/api/availability/{slot.Id}");
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "A törlés nem sikerült", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (!IsDisposed) await LoadListAsync();
        }
    }

    // ================================================================
    // PROFIL: a fiók adatai (szerepkör szerinti mezőkkel)
    // ================================================================
    internal class ProfilePage : PageBase
    {
        public ProfilePage() : base("Profil", "A fiókod adatai.") { }

        protected override async Task BuildAsync()
        {
            var account = await ApiClient.GetAsync<AccountDto>("/api/account");
            if (IsDisposed) return;

            // az oktatónak statisztika és értékelések is járnak; ha ezek nem töltődnek be, a profil enélkül is megjelenik
            List<BookingDto>? completed = null;
            List<ReviewItemDto>? reviews = null;
            if (account.Role == "TUTOR")
            {
                try
                {
                    var bookingsTask = ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=past");
                    var reviewsTask = ApiClient.GetAsync<List<ReviewItemDto>>($"/api/tutors/{account.Id}/reviews");
                    await Task.WhenAll(bookingsTask, reviewsTask);
                    completed = bookingsTask.Result;
                    reviews = reviewsTask.Result;
                }
                catch (ApiException)
                {
                    completed = null;
                    reviews = null;
                }
                if (IsDisposed) return;
            }

            ClearBody();

            var head = new CardPanel();
            var top = Ui.Row();
            top.Controls.Add(new AvatarCircle(account.Name, 72));

            var names = Ui.Row();
            names.FlowDirection = FlowDirection.TopDown;
            names.Controls.Add(Ui.Lbl(account.Name, 17F, Theme.Text, bold: true, margin: Ui.Pad(0, 6, 0, 2)));
            names.Controls.Add(Ui.Lbl(account.Email, 10.5F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 8)));
            bool verified = ApiClient.CurrentUser?.EmailVerified ?? true;
            var chipRow = Ui.Row();
            chipRow.Controls.Add(Ui.Chip(Ui.RoleName(account.Role), Theme.MainBlue));
            chipRow.Controls.Add(verified
                ? Ui.Chip("E-mail megerősítve", Theme.Success)
                : Ui.Chip("E-mail nincs megerősítve", Theme.Warning));
            names.Controls.Add(chipRow);
            top.Controls.Add(names);
            head.Controls.Add(top);
            head.Controls.Add(Ui.PrimaryBtn("Adatok szerkesztése", async (_, _) => await EditAsync(account)));
            if (!verified)
                head.Controls.Add(Ui.Btn("Megerősítő levél újraküldése", Theme.MainBlue, Theme.Text, async (_, _) => await ResendAsync(), outline: true));
            AddRow(head);

            if (!string.IsNullOrWhiteSpace(account.Bio))
            {
                var about = new CardPanel();
                about.Controls.Add(Ui.Lbl("Bemutatkozás", 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 8)));
                about.Controls.Add(Ui.Lbl(account.Bio, 10.5F, Theme.Muted, wrap: true, margin: Ui.Pad(0)));
                AddRow(about);
            }

            if (account.Role == "TUTOR")
            {
                var card = new CardPanel();
                card.Controls.Add(Ui.Lbl("Tantárgyaid és óradíjaid", 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 10)));
                if (account.Subjects == null || account.Subjects.Count == 0)
                {
                    card.Controls.Add(Ui.Lbl("Még nem adtál meg tantárgyat. A weboldalon a fiókod szerkesztésénél pótolhatod.", 10F, Theme.Muted, wrap: true, margin: Ui.Pad(0)));
                }
                else
                {
                    var chips = Ui.WrapRow();
                    foreach (var subject in account.Subjects)
                        chips.Controls.Add(Ui.Chip($"{subject.Name} · {Ui.Money(subject.HourlyRate)}/óra"));
                    card.Controls.Add(chips);
                }
                AddRow(card);

                if (completed != null) AddStats(completed, reviews);
                if (reviews != null) AddReviews(reviews);
            }

            if (account.Role == "STUDENT")
            {
                var card = new CardPanel();
                card.Controls.Add(Ui.Lbl("Tanulmányaid", 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 10)));
                card.Controls.Add(Ui.KeyValue("Iskolatípus", string.IsNullOrWhiteSpace(account.SchoolLevel) ? "–" : account.SchoolLevel!));
                card.Controls.Add(Ui.KeyValue("Évfolyam", string.IsNullOrWhiteSpace(account.Grade) ? "–" : account.Grade!));
                if (account.LearningSubjects is { Count: > 0 })
                {
                    card.Controls.Add(Ui.Lbl("Ebben kérnél segítséget", 10F, Theme.Muted, margin: Ui.Pad(0, 10, 0, 6)));
                    var chips = Ui.WrapRow();
                    foreach (var subject in account.LearningSubjects)
                        chips.Controls.Add(Ui.Chip(subject.Name));
                    card.Controls.Add(chips);
                }
                AddRow(card);
            }

            var session = new CardPanel();
            session.Controls.Add(Ui.Lbl("Munkamenet", 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 6)));
            session.Controls.Add(Ui.Lbl("Kijelentkezés után újra be kell jelentkezned az alkalmazás használatához.", 10F, Theme.Muted, wrap: true, margin: Ui.Pad(0, 0, 0, 8)));
            session.Controls.Add(Ui.Btn("Kijelentkezés", Theme.CardBg, Theme.Danger, (_, _) => RequestLogout(), outline: true));
            AddRow(session);

        }

        /// <summary>Megnyitja a szerkesztő ablakot; mentés után az oldal újratölt az új adatokkal.</summary>
        /// <summary>Teljesített órák, óraszám és bevétel (a szerver az utolsó 200 teljesített órát adja vissza).</summary>
        private void AddStats(List<BookingDto> completed, List<ReviewItemDto>? reviews)
        {
            AddRow(Ui.Section("Statisztikák"));

            double hours = completed.Sum(b => (b.EndTime - b.StartTime).TotalHours);
            decimal total = completed.Sum(b => b.Price ?? 0m);
            var now = DateTime.Now;
            decimal thisMonth = completed
                .Where(b => b.StartTime.ToLocalTime().Year == now.Year && b.StartTime.ToLocalTime().Month == now.Month)
                .Sum(b => b.Price ?? 0m);

            var tiles = Ui.WrapRow();
            tiles.Controls.Add(new StatTile(completed.Count, "", "Teljesített óra", "\uE787", Theme.MainBlue));
            tiles.Controls.Add(new StatTile((int)Math.Round(hours), " h", "Tartott órák hossza", "\uE121", Theme.Success));
            AddRow(tiles);

            var money = new CardPanel();
            money.Controls.Add(Ui.KeyValue("Bevétel összesen", Ui.Money(total)));
            money.Controls.Add(Ui.KeyValue("Bevétel ebben a hónapban", Ui.Money(thisMonth)));
            if (reviews is { Count: > 0 })
                money.Controls.Add(Ui.KeyValue("Átlagos értékelés", $"★ {reviews.Average(r => r.Rating).ToString("0.0", Ui.Hu)}  ({reviews.Count} értékelés)"));
            if (completed.Count >= 200)
                money.Controls.Add(Ui.Lbl("Az utolsó 200 teljesített óra alapján.", 9.5F, Theme.Muted, margin: Ui.Pad(0, 6, 0, 0)));
            AddRow(money);
        }

        private const int ReviewsShownFirst = 5;

        private void AddReviews(List<ReviewItemDto> reviews)
        {
            AddRow(Ui.Section("Értékelések"));

            var card = new CardPanel();
            if (reviews.Count == 0)
            {
                card.Controls.Add(Ui.Lbl("Még nem érkezett értékelésed.", 10.5F, Theme.Muted, margin: Ui.Pad(0)));
                AddRow(card);
                return;
            }

            foreach (var review in reviews.Take(ReviewsShownFirst))
                AddReview(card, review);

            if (reviews.Count > ReviewsShownFirst)
            {
                HoverButton? more = null;
                more = Ui.Btn($"Mind a(z) {reviews.Count} értékelés megjelenítése", Theme.CardBg, Theme.Text, (_, _) =>
                {
                    // a gomb a saját eltávolítását váltja ki, ezért a kattintás után hajtjuk végre
                    BeginInvoke(new Action(() =>
                    {
                        card.SuspendLayout();
                        card.Controls.Remove(more);
                        more!.Dispose();
                        foreach (var review in reviews.Skip(ReviewsShownFirst))
                            AddReview(card, review);
                        card.ResumeLayout(true);
                    }));
                }, outline: true);
                card.Controls.Add(more);
            }
            AddRow(card);
        }

        private static void AddReview(CardPanel card, ReviewItemDto review)
        {
            var row = Ui.Row();
            row.Controls.Add(Ui.Chip($"★ {review.Rating}", Theme.Warning));
            row.Controls.Add(Ui.Lbl(string.IsNullOrWhiteSpace(review.ReviewerName) ? "Diák" : review.ReviewerName!, 10.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 1, 12, 0)));
            row.Controls.Add(Ui.Lbl(review.CreatedAt is DateTime created ? created.ToLocalTime().ToString("yyyy. MM. dd.", Ui.Hu) : "", 9.5F, Theme.Muted, margin: Ui.Pad(0, 3, 0, 0)));
            card.Controls.Add(row);

            if (!string.IsNullOrWhiteSpace(review.Comment))
                card.Controls.Add(Ui.Lbl(review.Comment!, 10F, Theme.Muted, wrap: true, margin: Ui.Pad(0, 0, 0, 14)));
        }

        private async Task ResendAsync()
        {
            await VerifyUi.ResendAsync(FindForm());
            if (!IsDisposed) await LoadAsync();
        }

        private async Task EditAsync(AccountDto account)
        {
            try
            {
                var subjects = await ApiClient.GetCachedAsync<List<SubjectDto>>("/api/subjects");
                if (IsDisposed) return;

                using var dialog = new EditProfileDialog(account, subjects);
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK && !IsDisposed)
                    await LoadAsync();
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Nem sikerült megnyitni a szerkesztést", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    // ================================================================
    // BEÁLLÍTÁSOK: téma, animációk, fiók- és alkalmazásinfó
    // ================================================================
    internal class SettingsPage : PageBase
    {
        private readonly UserInfo user;

        public SettingsPage(UserInfo user) : base("Beállítások", "Szabd személyre az alkalmazást.")
        {
            this.user = user;
        }

        protected override Task BuildAsync()
        {
            ClearBody();

            var look = new CardPanel();
            look.Controls.Add(Ui.Lbl("Megjelenés", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 8)));
            look.Controls.Add(SwitchRow("Sötét mód", "Kíméletesebb a szemnek, különösen este.", Theme.Dark,
                // az oldal újraépül, ezért a váltást a kattintás kezelése után hajtjuk végre
                on => BeginInvoke(new Action(() => Theme.SetDark(on)))));
            look.Controls.Add(SwitchRow("Animációk", "Finom mozgások és átmenetek a felületen.", Anim.Enabled,
                on => Anim.SetEnabled(on)));
            AddRow(look);

            var notifications = new CardPanel();
            notifications.Controls.Add(Ui.Lbl("Értesítések", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 8)));
            notifications.Controls.Add(SwitchRow("Tálcaértesítések", "Új üzenetről és foglalás-változásról jelez, ha az ablak nincs előtérben.",
                AppSettings.NotificationsEnabled, on => AppSettings.SetNotifications(on)));
            if (user.Role == "STUDENT")
                notifications.Controls.Add(SwitchRow("Kedvenc oktatók új időpontjai", "Jelez, ha egy kedvenc oktatód új szabad időpontot hirdet (amíg az app fut).",
                    AppSettings.FavoriteAlertsEnabled, on => AppSettings.SetFavoriteAlerts(on)));
            AddRow(notifications);

            AddPrivacyCard();

            var account = new CardPanel();
            account.Controls.Add(Ui.Lbl("Fiók", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 10)));
            account.Controls.Add(Ui.KeyValue("Név", user.Name));
            account.Controls.Add(Ui.KeyValue("E-mail", user.Email));
            account.Controls.Add(Ui.KeyValue("Szerepkör", Ui.RoleName(user.Role)));
            AddRow(account);

            var app = new CardPanel();
            app.Controls.Add(Ui.Lbl("Alkalmazás", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 10)));
            app.Controls.Add(Ui.KeyValue("Szerver", ApiClient.BaseUrl));
            app.Controls.Add(Ui.Lbl("A szerver címét a TUTORCONNECT_URL környezeti változóval lehet módosítani.", 9.5F, Theme.Muted, wrap: true, margin: Ui.Pad(0, 6, 0, 0)));
            AddRow(app);

            return Task.CompletedTask;
        }

        // ------------------------------------------------------------
        // Adatkezelés: tájékoztatók, adatexport, mentett belépés, fióktörlés
        // ------------------------------------------------------------

        private void AddPrivacyCard()
        {
            var card = new CardPanel();
            card.Controls.Add(Ui.Lbl("Adatkezelés", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 6)));
            card.Controls.Add(Ui.Lbl("Az adataid a TutorConnect szerverén tárolódnak. Itt elolvashatod a tájékoztatókat, letöltheted az adataidat, vagy törölheted a fiókodat.",
                9.5F, Theme.Muted, wrap: true, margin: Ui.Pad(0, 0, 0, 10)));

            var documents = Ui.Row();
            documents.Controls.Add(Ui.Btn("Adatvédelmi tájékoztató", Theme.CardBg, Theme.Text, (_, _) => Web.Open("/adatvedelem"), outline: true));
            documents.Controls.Add(Ui.Btn("Felhasználási feltételek", Theme.CardBg, Theme.Text, (_, _) => Web.Open("/felhasz_feltetelek"), outline: true));
            card.Controls.Add(documents);

            var data = Ui.Row();
            data.Controls.Add(Ui.Btn("Adataim letöltése (JSON)", Theme.CardBg, Theme.Text, async (_, _) => await ExportDataAsync(), outline: true));
            data.Controls.Add(Ui.Btn("Mentett belépés törlése", Theme.CardBg, Theme.Text, (_, _) =>
            {
                ApiClient.RememberSession(false);
                MessageBox.Show(FindForm(), "A mentett belépést töröltük. Következő induláskor újra be kell jelentkezned.",
                    "Mentett belépés", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }, outline: true));
            card.Controls.Add(data);

            if (user.Role != "ADMIN")
            {
                card.Controls.Add(Ui.Lbl("Veszélyes zóna", 10.5F, Theme.Danger, bold: true, margin: Ui.Pad(0, 12, 0, 6)));
                card.Controls.Add(Ui.Btn("Fiók törlése", Theme.CardBg, Theme.Danger, (_, _) => DeleteAccount(), outline: true));
            }
            AddRow(card);
        }

        private void DeleteAccount()
        {
            using var dialog = new DeleteAccountDialog();
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

            MessageBox.Show(FindForm(), "A fiókod törölve lett. Köszönjük, hogy használtad a TutorConnectet!", "Fiók törölve",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            RequestLogout(); // a szerver már megszüntette a munkamenetet: vissza a belépőhöz
        }

        /// <summary>A saját fiók- és foglalási adatok mentése egy JSON-fájlba.</summary>
        private async Task ExportDataAsync()
        {
            try
            {
                var sections = new List<(string Name, string Json)> { ("fiok", await ApiClient.GetRawAsync("/api/account")) };
                foreach (string status in new[] { "pending", "upcoming", "past", "cancelled" })
                    sections.Add(($"foglalasok_{status}", await ApiClient.GetRawAsync($"/api/bookings?status={status}")));
                if (IsDisposed) return;

                using var dialog = new SaveFileDialog
                {
                    Title = "Adataim mentése",
                    Filter = "JSON fájl (*.json)|*.json",
                    FileName = "tutorconnect-adataim.json"
                };
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

                using (var stream = File.Create(dialog.FileName))
                using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions
                {
                    Indented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }))
                {
                    writer.WriteStartObject();
                    writer.WriteString("exportalva", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    foreach (var (name, json) in sections)
                    {
                        using var document = System.Text.Json.JsonDocument.Parse(json);
                        writer.WritePropertyName(name);
                        document.RootElement.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }

                MessageBox.Show(FindForm(), "Az adataid elmentve.", "Adatexport", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Nem sikerült letölteni az adatokat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                MessageBox.Show(FindForm(), "A fájl mentése nem sikerült: " + ex.Message, "Nem sikerült letölteni az adatokat",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Egy beállítás-sor: bal oldalt cím és leírás, jobb oldalt a kapcsoló.</summary>
        private static Control SwitchRow(string title, string description, bool initial, Action<bool> onChanged)
        {
            var table = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Height = Ui.S(62),
                BackColor = Color.Transparent,
                Margin = Ui.Pad(0, 0, 0, 4),
                Tag = "fill" // a CardPanel a teljes belső szélességre húzza
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Ui.S(70)));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var text = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Left,
                Margin = Ui.Pad(0)
            };
            text.Controls.Add(Ui.Lbl(title, 11F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 2)));
            text.Controls.Add(Ui.Lbl(description, 9.5F, Theme.Muted, margin: Ui.Pad(0)));

            var toggle = new ToggleSwitch(initial) { Anchor = AnchorStyles.Right, Margin = Ui.Pad(0) };
            toggle.Toggled += (_, _) => onChanged(toggle.IsOn);

            table.Controls.Add(text, 0, 0);
            table.Controls.Add(toggle, 1, 0);
            return table;
        }
    }

    /// <summary>Az e-mail megerősítő levél újraküldése – a profil és a felső sáv közös logikája.</summary>
    internal static class VerifyUi
    {
        public static async Task ResendAsync(IWin32Window? owner)
        {
            try
            {
                bool already = await ApiClient.ResendVerificationAsync();
                MessageBox.Show(owner,
                    already
                        ? "Az e-mail címed már meg van erősítve."
                        : "Elküldtük a megerősítő levelet. Nyisd meg a levélben lévő linket egy böngészőben, utána az app magától frissül.",
                    "E-mail megerősítése", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(owner, ex.Message, "Nem sikerült elküldeni", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}

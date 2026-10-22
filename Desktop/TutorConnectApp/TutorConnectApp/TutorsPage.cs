using System.Globalization;

namespace TutorConnectApp
{
    // ================================================================
    // OKTATÓK (diák): keresés, tantárgy / értékelés szűrő, rendezés + időpontfoglalás
    // ================================================================
    internal class TutorsPage : PageBase
    {
        /// <summary>A Tantárgyak oldal ezzel nyitja meg az oktatókat egy adott tantárgyra szűrve.</summary>
        public static string? InitialSubject;

        private const string AllSubjects = "Összes tantárgy";

        private static readonly string[] SortOptions =
            { "Alapértelmezett", "Legjobb értékelés", "Ár szerint növekvő", "Ár szerint csökkenő" };

        private List<TutorDto> tutors = new();
        private SearchBox search = null!;
        private DropdownSelect subjectBox = null!;
        private NumberField ratingBox = null!;
        private DropdownSelect sortBox = null!;
        private NumberField minPriceBox = null!;
        private NumberField maxPriceBox = null!;
        private CheckPill favoritesBox = null!;
        private Label countLabel = null!;
        private State? restored;
        private int limit = PageSize;
        private readonly Debouncer searchDebounce;

        /// <summary>Ennyi oktatókártyát rajzolunk egyszerre; a többi gombnyomásra jelenik meg.</summary>
        private const int PageSize = 12;

        private sealed record State(string Query, int Subject, string Rating, int Sort, string MinPrice, string MaxPrice, bool OnlyFavorites);

        public override object? CaptureState() =>
            search == null
                ? null
                : new State(search.Value, subjectBox.SelectedIndex, ratingBox.RawText, sortBox.SelectedIndex,
                    minPriceBox.RawText, maxPriceBox.RawText, favoritesBox.Checked);

        public override void RestoreState(object? state) => restored = state as State;

        public override bool HasPendingRequest => InitialSubject != null;

        public override bool FocusSearch()
        {
            if (search == null) return false;
            search.FocusInput();
            return true;
        }
        public override bool InvalidatesOthers => true;

        public TutorsPage() : base("Oktatók", "Válaszd ki a hozzád illő oktatót, és foglalj időpontot.")
        {
            // a találatok a gépelés szünetében frissülnek, nem minden betűnél
            searchDebounce = new Debouncer(160, () =>
            {
                limit = PageSize;
                RenderList(animate: false);
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) searchDebounce.Dispose();
            base.Dispose(disposing);
        }

        protected override async Task BuildAsync()
        {
            var tutorsTask = ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
            var subjectsTask = ApiClient.GetCachedAsync<List<SubjectDto>>("/api/subjects");
            var accountTask = TryGetAccountAsync(); // az ajánláshoz; hiba esetén nincs ajánlás, az oldal így is működik
            await Task.WhenAll(tutorsTask, subjectsTask, accountTask);
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

            AddRecommendations(accountTask.Result);

            search = new SearchBox("Keresés oktató neve vagy tantárgy alapján...");
            if (keep != null) search.SetValue(keep.Query); // még a figyelő bekötése előtt, hogy ne rajzoljon
            search.ValueChanged += (_, _) => searchDebounce.Trigger();
            AddRow(search);

            subjectBox = new DropdownSelect("Tantárgy", new[] { AllSubjects }.Concat(subjectNames), initial);
            sortBox = new DropdownSelect("Rendezés", SortOptions, keep?.Sort ?? 0);
            foreach (var box in new[] { subjectBox, sortBox })
                box.SelectionChanged += (_, _) =>
                {
                    limit = PageSize;
                    RenderList(animate: false);
                };

            // amit a felhasználó keres, azt beírja: óradíj-tartomány és legkisebb értékelés (üresen: nincs korlát)
            minPriceBox = new NumberField("Óradíj legalább", "pl. 3000", "Ft", 0, 1_000_000);
            maxPriceBox = new NumberField("Óradíj legfeljebb", "pl. 8000", "Ft", 0, 1_000_000);
            ratingBox = new NumberField("Értékelés legalább", "pl. 4,5", "★", 0, 5, decimals: true);
            if (keep != null)
            {
                minPriceBox.SetText(keep.MinPrice);
                maxPriceBox.SetText(keep.MaxPrice);
                ratingBox.SetText(keep.Rating);
            }
            foreach (var field in new[] { minPriceBox, maxPriceBox, ratingBox })
                field.ValueChanged += (_, _) => searchDebounce.Trigger(); // gépelés közben a szünetben szűr

            favoritesBox = new CheckPill("Csak a kedvenceim", "♥", keep?.OnlyFavorites ?? false);
            favoritesBox.CheckedChanged += (_, _) =>
            {
                limit = PageSize;
                RenderList(animate: false);
            };

            var options = Ui.WrapRow();
            options.Controls.Add(subjectBox);
            options.Controls.Add(sortBox);
            options.Controls.Add(favoritesBox);
            AddRow(options);

            var ranges = Ui.WrapRow();
            ranges.Controls.Add(minPriceBox);
            ranges.Controls.Add(maxPriceBox);
            ranges.Controls.Add(ratingBox);
            AddRow(ranges);

            countLabel = Ui.Lbl("", 10F, Theme.Muted, margin: Ui.Pad(2, 0, 0, 12));
            AddRow(countLabel);

            RenderList(animate: true);
        }

        private static async Task<AccountDto?> TryGetAccountAsync()
        {
            try
            {
                return await ApiClient.GetAsync<AccountDto>("/api/account");
            }
            catch (ApiException)
            {
                return null;
            }
        }

        /// <summary>Az oktató tantárgyai közül azok, amelyeket a diák tanulni szeretne.</summary>
        private static List<string> MatchedSubjects(TutorDto tutor, HashSet<string> wanted) =>
            (tutor.Subjects ?? "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(name => wanted.Contains(name.ToLowerInvariant()))
                .ToList();

        /// <summary>
        /// "Neked ajánljuk": a diák által megadott tanulni kívánt tantárgyak alapján a legjobban illő három oktató
        /// (több egyező tantárgy, jobb értékelés előrébb). Az oktatóknál nincs évfolyam-adat, ezért az csak tájékoztató.
        /// </summary>
        private void AddRecommendations(AccountDto? account)
        {
            if (account == null || account.Role != "STUDENT") return;

            AddRow(Ui.Section("Neked ajánljuk"));

            var wanted = (account.LearningSubjects ?? new List<NamedDto>())
                .Select(subject => subject.Name.Trim().ToLowerInvariant())
                .Where(name => name.Length > 0)
                .ToHashSet();

            if (wanted.Count == 0)
            {
                var hint = new CardPanel();
                hint.Controls.Add(Ui.Lbl("Add meg a profilodban, miből kérnél segítséget, és ide a hozzád illő oktatókat tesszük.", 10.5F, Theme.Muted, wrap: true, margin: Ui.Pad(0, 0, 0, 10)));
                hint.Controls.Add(Ui.PrimaryBtn("Tanulmányaim megadása", (_, _) => Navigate("profile")));
                AddRow(hint);
                AddRow(Ui.Section("Minden oktató"));
                return;
            }

            var picks = tutors
                .Select(t => (Tutor: t, Match: MatchedSubjects(t, wanted)))
                .Where(x => x.Match.Count > 0)
                .OrderByDescending(x => x.Match.Count * 10 + (x.Tutor.ReviewCount > 0 ? (double)x.Tutor.AverageRating : 3.0) + Math.Min(x.Tutor.ReviewCount, 20) * 0.05)
                .Take(3)
                .ToList();

            if (picks.Count == 0)
            {
                AddRow(Ui.MessageCard("Most nincs olyan oktató, aki a megadott tantárgyaidat tanítja. Nézz körül lent!", ""));
                AddRow(Ui.Section("Minden oktató"));
                return;
            }

            string basis = string.Join(" · ", new[] { account.SchoolLevel, account.Grade }.Where(text => !string.IsNullOrWhiteSpace(text)));
            AddRow(Ui.Lbl("A tanulni kívánt tantárgyaid alapján" + (basis.Length > 0 ? $" ({basis})" : "") + ":", 10F, Theme.Muted, margin: Ui.Pad(2, 0, 0, 12)));

            var row = Ui.WrapRow();
            foreach (var (tutor, match) in picks)
                row.Controls.Add(BuildRecommendCard(tutor, match));
            AddRow(row);

            AddRow(Ui.Section("Minden oktató"));
        }

        private CardPanel BuildRecommendCard(TutorDto tutor, List<string> matches)
        {
            var card = new AccentCard(Theme.Success);
            card.MinimumSize = new Size(Ui.S(350), 0);
            card.MaximumSize = new Size(Ui.S(350), 0);
            card.Margin = Ui.Pad(0, 0, 16, 16);

            var head = Ui.Row();
            head.Controls.Add(new AvatarCircle(tutor.FullName, 52));

            var info = Ui.Row();
            info.FlowDirection = FlowDirection.TopDown;
            info.Controls.Add(Ui.Lbl(tutor.FullName, 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 2, 0, 2)));
            info.Controls.Add(Ui.Lbl(
                tutor.ReviewCount > 0
                    ? $"{Stars(tutor.AverageRating)}  {tutor.AverageRating.ToString("0.0", Ui.Hu)} ({tutor.ReviewCount})"
                    : "Még nincs értékelés",
                10F, tutor.ReviewCount > 0 ? Theme.Star : Theme.Muted, margin: Ui.Pad(0)));
            string range = PriceRange(tutor);
            if (range.Length > 0)
                info.Controls.Add(Ui.Lbl(range, 10F, Theme.MainBlue, bold: true, margin: Ui.Pad(0, 2, 0, 0)));
            head.Controls.Add(info);
            card.Controls.Add(head);

            // az egyező tantárgyak zölden: ezért ajánljuk
            var chips = Ui.WrapRow();
            foreach (string match in matches)
                chips.Controls.Add(Ui.Chip(match, Theme.Success));
            card.Controls.Add(chips);

            var actions = Ui.Row();
            actions.Controls.Add(Ui.Btn("Profil", Theme.CardBg, Theme.MainBlue, async (_, _) => await OpenProfileAsync(tutor), outline: true));
            actions.Controls.Add(Ui.PrimaryBtn("Foglalás", (_, _) => OpenBooking(tutor)));
            card.Controls.Add(actions);
            return card;
        }


        /// <summary>Az oktató legkisebb és legnagyobb óradíja; (0, 0), ha nincs megadva.</summary>
        internal static (decimal Min, decimal Max) PriceBounds(TutorDto tutor)
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
            decimal minRating = ratingBox.Value ?? 0m;
            int minPrice = (int)(minPriceBox.Value ?? 0m);
            int maxPrice = (int)(maxPriceBox.Value ?? 0m);
            bool onlyFavorites = favoritesBox.Checked;

            IEnumerable<TutorDto> filtered = tutors.Where(t =>
                (query.Length == 0
                    || TextSearch.Contains(t.FullName, query)
                    || TextSearch.Contains(t.Subjects ?? "", query))
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

            ReplaceRowsAfter(countLabel, () =>
            {
                if (shown.Count == 0)
                {
                    AddRow(Ui.MessageCard("Nincs a szűrőknek megfelelő oktató.", "\uE721"), animate);
                    return;
                }

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
            });
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
                    if (favoritesBox.Checked && !now)
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
            var (min, max) = PriceBounds(tutor);
            if (max == 0) return "";

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
}

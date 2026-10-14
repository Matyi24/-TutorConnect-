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
            head.Controls.Add(Ui.Lbl(b.SubjectName ?? "Tantárgy", 12F, Theme.Text, bold: true, margin: new Padding(0, 2, 12, 0)));
            head.Controls.Add(Ui.Chip("Visszaigazolva", Ui.Success));
            card.Controls.Add(head);
            card.Controls.Add(Ui.Lbl((b.AsStudent ? "Oktató: " : "Diák: ") + (b.OtherName ?? "ismeretlen"), 10.5F, Theme.Text));
            card.Controls.Add(Ui.Lbl(Ui.FormatRange(b.StartTime, b.EndTime), 10F, Theme.Muted, margin: new Padding(0)));
            return card;
        }
    }

    // ================================================================
    // TANTÁRGYAK: kategóriák szerint csoportosítva
    // ================================================================
    internal class SubjectsPage : PageBase
    {
        public SubjectsPage() : base("Tantárgyak", "Ezekben a témákban kereshetsz oktatót.") { }

        protected override async Task BuildAsync()
        {
            var subjects = await ApiClient.GetAsync<List<SubjectDto>>("/api/subjects");
            if (IsDisposed) return;

            ClearBody();
            if (subjects.Count == 0)
            {
                AddRow(Ui.MessageCard("Még nincs felvett tantárgy.", ""));
                return;
            }

            foreach (var group in subjects.GroupBy(s => s.Category).OrderBy(g => g.Key))
            {
                var card = new CardPanel();
                var head = Ui.Row();
                head.Controls.Add(Ui.Lbl(group.Key, 12.5F, Theme.Text, bold: true, margin: new Padding(0, 0, 10, 10)));
                head.Controls.Add(Ui.Lbl($"{group.Count()} tantárgy", 9.5F, Theme.Muted, margin: new Padding(0, 3, 0, 10)));
                card.Controls.Add(head);

                var chips = Ui.WrapRow();
                foreach (var subject in group.OrderBy(s => s.Name))
                    chips.Controls.Add(Ui.Chip(subject.Name));
                card.Controls.Add(chips);

                AddRow(card);
            }
        }
    }

    // ================================================================
    // OKTATÓK (diák): keresés + időpontfoglalás
    // ================================================================
    internal class TutorsPage : PageBase
    {
        private List<TutorDto> tutors = new();
        private TextBox search = null!;

        public TutorsPage() : base("Oktatók", "Válaszd ki a hozzád illő oktatót, és foglalj időpontot.") { }

        protected override async Task BuildAsync()
        {
            tutors = await ApiClient.GetAsync<List<TutorDto>>("/api/tutors");
            if (IsDisposed) return;

            ClearBody();
            search = new TextBox
            {
                PlaceholderText = "Keresés név vagy tantárgy alapján...",
                Font = Ui.MakeFont(11.5F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                Margin = new Padding(0, 0, 0, 18)
            };
            search.TextChanged += (_, _) => RenderList(animate: false);
            AddRow(search);
            RenderList(animate: true);
        }

        private void RenderList(bool animate)
        {
            Body.SuspendLayout();
            RemoveRowsAfter(search);

            string query = search.Text.Trim();
            var shown = tutors.Where(t =>
                    query.Length == 0
                    || t.FullName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || (t.Subjects ?? "").Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            if (shown.Count == 0)
                AddRow(Ui.MessageCard("Nincs a keresésnek megfelelő oktató.", ""), animate);
            else
                foreach (var tutor in shown)
                    AddRow(BuildCard(tutor), animate);

            Body.ResumeLayout(true);
        }

        private CardPanel BuildCard(TutorDto tutor)
        {
            var card = new CardPanel();

            var head = Ui.Row();
            head.Controls.Add(new AvatarCircle(tutor.FullName, 50));
            head.Controls.Add(Ui.Lbl(tutor.FullName, 13.5F, Theme.Text, bold: true, margin: new Padding(0, 13, 14, 0)));
            if (tutor.ReviewCount > 0)
            {
                var rating = Ui.Chip($"★ {tutor.AverageRating.ToString("0.0", Ui.Hu)}  ({tutor.ReviewCount})", Theme.Warning);
                rating.Margin = new Padding(0, 12, 0, 0);
                head.Controls.Add(rating);
            }
            card.Controls.Add(head);

            var chips = Ui.WrapRow();
            foreach (var (name, price) in ParseSubjects(tutor.SubjectPrices))
                chips.Controls.Add(Ui.Chip(price > 0 ? $"{name} · {Ui.Money(price)}/óra" : name));
            card.Controls.Add(chips);

            if (!string.IsNullOrWhiteSpace(tutor.Bio))
                card.Controls.Add(Ui.Lbl(tutor.Bio!, 10F, Theme.Muted, wrap: true, margin: new Padding(0, 4, 0, 4)));

            card.Controls.Add(Ui.PrimaryBtn("Időpont foglalása", (_, _) => OpenBooking(tutor)));
            return card;
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

        private static IEnumerable<(string Name, decimal Price)> ParseSubjects(string? raw)
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

            if (user.Role == "STUDENT")
            {
                var top = Ui.Row();
                top.Margin = new Padding(0, 0, 0, 12);
                top.Controls.Add(Ui.PrimaryBtn("+  Új foglalás", (_, _) => Navigate("tutors")));
                AddRow(top);
            }

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
            head.Controls.Add(Ui.Lbl(b.SubjectName ?? "Tantárgy", 12.5F, Theme.Text, bold: true, margin: new Padding(0, 2, 12, 0)));
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

            return card;
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
            top.Margin = new Padding(0, 0, 0, 12);
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
                    card.Controls.Add(Ui.Lbl(Ui.FormatDay(day.First().StartTime), 11.5F, Theme.Text, bold: true, margin: new Padding(0, 0, 0, 8)));

                    foreach (var slot in day)
                    {
                        var row = Ui.Row();
                        row.Controls.Add(Ui.Lbl(Ui.FormatTimeRange(slot.StartTime, slot.EndTime), 10.5F, Theme.Text, margin: new Padding(0, 9, 16, 0)));
                        row.Controls.Add(slot.IsBooked
                            ? Ui.Chip("Lefoglalva", Ui.Warning)
                            : Ui.Chip("Szabad", Ui.Success));
                        if (!slot.IsBooked)
                        {
                            var delete = Ui.Btn("Törlés", Theme.CardBg, Ui.Danger, async (_, _) => await DeleteSlotAsync(slot), outline: true);
                            delete.Margin = new Padding(8, 0, 0, 0);
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

            ClearBody();

            var head = new CardPanel();
            var top = Ui.Row();
            top.Controls.Add(new AvatarCircle(account.Name, 72));

            var names = Ui.Row();
            names.FlowDirection = FlowDirection.TopDown;
            names.Controls.Add(Ui.Lbl(account.Name, 17F, Theme.Text, bold: true, margin: new Padding(0, 6, 0, 2)));
            names.Controls.Add(Ui.Lbl(account.Email, 10.5F, Theme.Muted, margin: new Padding(0, 0, 0, 8)));
            names.Controls.Add(Ui.Chip(Ui.RoleName(account.Role), Theme.MainBlue));
            top.Controls.Add(names);
            head.Controls.Add(top);
            AddRow(head);

            if (!string.IsNullOrWhiteSpace(account.Bio))
            {
                var about = new CardPanel();
                about.Controls.Add(Ui.Lbl("Bemutatkozás", 12F, Theme.Text, bold: true, margin: new Padding(0, 0, 0, 8)));
                about.Controls.Add(Ui.Lbl(account.Bio, 10.5F, Theme.Muted, wrap: true, margin: new Padding(0)));
                AddRow(about);
            }

            if (account.Role == "TUTOR")
            {
                var card = new CardPanel();
                card.Controls.Add(Ui.Lbl("Tantárgyaid és óradíjaid", 12F, Theme.Text, bold: true, margin: new Padding(0, 0, 0, 10)));
                if (account.Subjects == null || account.Subjects.Count == 0)
                {
                    card.Controls.Add(Ui.Lbl("Még nem adtál meg tantárgyat. A weboldalon a fiókod szerkesztésénél pótolhatod.", 10F, Theme.Muted, wrap: true, margin: new Padding(0)));
                }
                else
                {
                    var chips = Ui.WrapRow();
                    foreach (var subject in account.Subjects)
                        chips.Controls.Add(Ui.Chip($"{subject.Name} · {Ui.Money(subject.HourlyRate)}/óra"));
                    card.Controls.Add(chips);
                }
                AddRow(card);
            }

            if (account.Role == "STUDENT")
            {
                var card = new CardPanel();
                card.Controls.Add(Ui.Lbl("Tanulmányaid", 12F, Theme.Text, bold: true, margin: new Padding(0, 0, 0, 10)));
                card.Controls.Add(Ui.KeyValue("Iskolatípus", string.IsNullOrWhiteSpace(account.SchoolLevel) ? "–" : account.SchoolLevel!));
                card.Controls.Add(Ui.KeyValue("Évfolyam", string.IsNullOrWhiteSpace(account.Grade) ? "–" : account.Grade!));
                if (account.LearningSubjects is { Count: > 0 })
                {
                    card.Controls.Add(Ui.Lbl("Ebben kérnél segítséget", 10F, Theme.Muted, margin: new Padding(0, 10, 0, 6)));
                    var chips = Ui.WrapRow();
                    foreach (var subject in account.LearningSubjects)
                        chips.Controls.Add(Ui.Chip(subject.Name));
                    card.Controls.Add(chips);
                }
                AddRow(card);
            }

            AddRow(Ui.Lbl("A profil szerkesztése egyelőre a weboldalon érhető el.", 9.5F, Theme.Muted));
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
            look.Controls.Add(Ui.Lbl("Megjelenés", 12.5F, Theme.Text, bold: true, margin: new Padding(0, 0, 0, 8)));
            look.Controls.Add(SwitchRow("Sötét mód", "Kíméletesebb a szemnek, különösen este.", Theme.Dark,
                // az oldal újraépül, ezért a váltást a kattintás kezelése után hajtjuk végre
                on => BeginInvoke(new Action(() => Theme.SetDark(on)))));
            look.Controls.Add(SwitchRow("Animációk", "Finom mozgások és átmenetek a felületen.", Anim.Enabled,
                on => Anim.SetEnabled(on)));
            AddRow(look);

            var account = new CardPanel();
            account.Controls.Add(Ui.Lbl("Fiók", 12.5F, Theme.Text, bold: true, margin: new Padding(0, 0, 0, 10)));
            account.Controls.Add(Ui.KeyValue("Név", user.Name));
            account.Controls.Add(Ui.KeyValue("E-mail", user.Email));
            account.Controls.Add(Ui.KeyValue("Szerepkör", Ui.RoleName(user.Role)));
            AddRow(account);

            var app = new CardPanel();
            app.Controls.Add(Ui.Lbl("Alkalmazás", 12.5F, Theme.Text, bold: true, margin: new Padding(0, 0, 0, 10)));
            app.Controls.Add(Ui.KeyValue("Szerver", ApiClient.BaseUrl));
            app.Controls.Add(Ui.Lbl("A szerver címét a TUTORCONNECT_URL környezeti változóval lehet módosítani.", 9.5F, Theme.Muted, wrap: true, margin: new Padding(0, 6, 0, 0)));
            AddRow(app);

            return Task.CompletedTask;
        }

        /// <summary>Egy beállítás-sor: bal oldalt cím és leírás, jobb oldalt a kapcsoló.</summary>
        private static Control SwitchRow(string title, string description, bool initial, Action<bool> onChanged)
        {
            var table = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Height = 62,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 4),
                Tag = "fill" // a CardPanel a teljes belső szélességre húzza
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var text = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0)
            };
            text.Controls.Add(Ui.Lbl(title, 11F, Theme.Text, bold: true, margin: new Padding(0, 0, 0, 2)));
            text.Controls.Add(Ui.Lbl(description, 9.5F, Theme.Muted, margin: new Padding(0)));

            var toggle = new ToggleSwitch(initial) { Anchor = AnchorStyles.Right, Margin = new Padding(0) };
            toggle.Toggled += (_, _) => onChanged(toggle.IsOn);

            table.Controls.Add(text, 0, 0);
            table.Controls.Add(toggle, 1, 0);
            return table;
        }
    }

    // ================================================================
    // CHAT: egyelőre csak jelzés (a weben ez egy nagy, valós idejű modul)
    // ================================================================
    internal class ChatPage : PageBase
    {
        public ChatPage() : base("Chat", "Beszélgess az oktatóiddal és a diákjaiddal.") { }

        protected override Task BuildAsync()
        {
            ClearBody();
            AddRow(Ui.MessageCard("A chat hamarosan elérhető lesz az alkalmazásban. Addig használd a weboldalon.", ""));
            return Task.CompletedTask;
        }
    }
}

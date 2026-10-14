using System.Globalization;

namespace TutorConnectApp
{
    // ================================================================
    // FŐOLDAL: üdvözlés, saját foglalások összesítője, oldal statisztikák
    // ================================================================
    internal class HomePage : PageBase
    {
        private readonly UserInfo user;

        public HomePage(UserInfo user) : base($"Üdv, {Ui.FirstName(user.Name)}!", Ui.RoleIntro(user.Role))
        {
            this.user = user;
        }

        protected override async Task BuildAsync()
        {
            var statsTask = ApiClient.GetAsync<StatsDto>("/api/stats");
            var summaryTask = ApiClient.GetAsync<BookingSummaryDto>("/api/bookings/summary");
            await Task.WhenAll(statsTask, summaryTask);
            if (IsDisposed) return;

            var stats = statsTask.Result;
            var summary = summaryTask.Result;
            bool tutor = user.Role == "TUTOR";

            ClearBody();

            AddRow(Ui.Section("Foglalásaid"));
            var mine = Ui.WrapRow();
            mine.Controls.Add(Ui.StatTile(summary.Pending.ToString(), tutor ? "Jóváhagyásra vár" : "Függőben", Ui.Warning));
            mine.Controls.Add(Ui.StatTile(summary.Upcoming.ToString(), "Közelgő óra", Ui.Success));
            mine.Controls.Add(Ui.StatTile(summary.Past.ToString(), "Lezajlott óra", Theme.MainBlue));
            mine.Controls.Add(Ui.StatTile(summary.Cancelled.ToString(), "Lemondott", Ui.Muted));
            AddRow(mine);

            AddRow(Ui.Section("A TutorConnect számokban"));
            var site = Ui.WrapRow();
            site.Controls.Add(Ui.StatTile(stats.Tutors.ToString(), "Oktató", Theme.MainBlueDark));
            site.Controls.Add(Ui.StatTile(stats.Bookings.ToString(), "Foglalás", Theme.MainBlueDark));
            site.Controls.Add(Ui.StatTile(stats.Satisfaction + "%", "Elégedettség", Theme.MainBlueDark));
            AddRow(site);
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
                AddRow(Ui.MessageCard("Még nincs felvett tantárgy."));
                return;
            }

            foreach (var group in subjects.GroupBy(s => s.Category).OrderBy(g => g.Key))
            {
                var card = new CardPanel();
                card.Controls.Add(Ui.Lbl(group.Key, 12F, Theme.DarkText, bold: true, margin: new Padding(0, 0, 0, 10)));

                var chips = Ui.WrapRow();
                foreach (var subject in group.OrderBy(s => s.Name))
                    chips.Controls.Add(Ui.Chip(subject.Name, Theme.MainBlueLight, Theme.MainBlueDark));
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
                Font = Ui.MakeFont(11F),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 16)
            };
            search.TextChanged += (_, _) => RenderList();
            AddRow(search);
            RenderList();
        }

        private void RenderList()
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
                AddRow(Ui.MessageCard("Nincs a keresésnek megfelelő oktató."));
            else
                foreach (var tutor in shown)
                    AddRow(BuildCard(tutor));

            Body.ResumeLayout(true);
        }

        private CardPanel BuildCard(TutorDto tutor)
        {
            var card = new CardPanel();

            var head = Ui.Row();
            head.Controls.Add(new AvatarCircle(tutor.FullName, 46));
            head.Controls.Add(Ui.Lbl(tutor.FullName, 13F, Theme.DarkText, bold: true, margin: new Padding(0, 11, 12, 0)));
            if (tutor.ReviewCount > 0)
            {
                var rating = Ui.Chip($"★ {tutor.AverageRating.ToString("0.0", Ui.Hu)} ({tutor.ReviewCount})", Theme.MainBlueLight, Theme.MainBlueDark);
                rating.Margin = new Padding(0, 10, 0, 0);
                head.Controls.Add(rating);
            }
            card.Controls.Add(head);

            var chips = Ui.WrapRow();
            foreach (var (name, price) in ParseSubjects(tutor.SubjectPrices))
                chips.Controls.Add(Ui.Chip(price > 0 ? $"{name} · {Ui.Money(price)}/óra" : name, Theme.MainBlueLight, Theme.MainBlueDark));
            card.Controls.Add(chips);

            if (!string.IsNullOrWhiteSpace(tutor.Bio))
                card.Controls.Add(Ui.Lbl(tutor.Bio!, 10F, Ui.Muted, wrap: true, margin: new Padding(0, 2, 0, 4)));

            card.Controls.Add(Ui.Btn("Időpont foglalása", Theme.MainBlue, Theme.White, (_, _) => OpenBooking(tutor)));
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
    // FOGLALÁSOK (diák és oktató): állapot szerinti fülek, műveletek
    // ================================================================
    internal class BookingsPage : PageBase
    {
        private static readonly (string Key, string Label)[] Tabs =
        {
            ("pending", "Függőben"),
            ("upcoming", "Közelgő"),
            ("past", "Korábbi"),
            ("cancelled", "Lemondott")
        };

        private readonly UserInfo user;
        private readonly Dictionary<string, Button> tabButtons = new();
        private BookingSummaryDto summary = new();
        private FlowLayoutPanel tabs = null!;
        private string filter = "";
        private int loadId;

        public BookingsPage(UserInfo user) : base(
            user.Role == "TUTOR" ? "Foglalások" : "Foglalásaim",
            user.Role == "TUTOR" ? "Fogadd el vagy utasítsd el a diákok foglalási kérelmeit." : "Kövesd nyomon az óráidat, és kezeld a foglalásaidat.")
        {
            this.user = user;
        }

        protected override async Task BuildAsync()
        {
            summary = await ApiClient.GetAsync<BookingSummaryDto>("/api/bookings/summary");
            if (IsDisposed) return;

            if (filter.Length == 0)
                filter = user.Role == "TUTOR" && summary.Pending > 0 ? "pending" : "upcoming";

            ClearBody();
            tabButtons.Clear();

            tabs = Ui.WrapRow();
            foreach (var (key, label) in Tabs)
            {
                string tabKey = key;
                var button = Ui.Btn(label, Theme.White, Theme.DarkText, async (_, _) =>
                {
                    filter = tabKey;
                    await LoadListAsync();
                }, outline: true);
                button.Margin = new Padding(0, 0, 8, 12);
                tabButtons[key] = button;
                tabs.Controls.Add(button);
            }
            AddRow(tabs);

            await LoadListAsync();
        }

        private int CountFor(string key) => key switch
        {
            "pending" => summary.Pending,
            "upcoming" => summary.Upcoming,
            "past" => summary.Past,
            "cancelled" => summary.Cancelled,
            _ => 0
        };

        private void UpdateTabs()
        {
            foreach (var (key, label) in Tabs)
            {
                var button = tabButtons[key];
                bool active = key == filter;
                button.Text = $"{label} ({CountFor(key)})";
                button.BackColor = active ? Theme.MainBlue : Theme.White;
                button.ForeColor = active ? Theme.White : Theme.DarkText;
                button.FlatAppearance.MouseOverBackColor = active ? Theme.MainBlueDark : Theme.LightGrey;
            }
        }

        private async Task LoadListAsync()
        {
            int myLoad = ++loadId;
            UpdateTabs();
            RemoveRowsAfter(tabs);
            AddRow(Ui.Lbl("Betöltés...", 10.5F, Ui.Muted));

            List<BookingDto> bookings;
            try
            {
                bookings = await ApiClient.GetAsync<List<BookingDto>>($"/api/bookings?status={filter}");
            }
            catch (ApiException ex)
            {
                if (IsDisposed || myLoad != loadId) return;
                RemoveRowsAfter(tabs);
                AddRow(Ui.MessageCard(ex.Message));
                return;
            }

            if (IsDisposed || myLoad != loadId) return;

            Body.SuspendLayout();
            RemoveRowsAfter(tabs);
            if (bookings.Count == 0)
                AddRow(Ui.MessageCard("Ebben a kategóriában nincs foglalás."));
            else
                foreach (var booking in bookings)
                    AddRow(BuildCard(booking));
            Body.ResumeLayout(true);
        }

        private CardPanel BuildCard(BookingDto b)
        {
            var card = new CardPanel();

            var head = Ui.Row();
            head.Controls.Add(Ui.Lbl(b.SubjectName ?? "Tantárgy", 12.5F, Theme.DarkText, bold: true, margin: new Padding(0, 2, 12, 0)));
            head.Controls.Add(StatusChip(b.Status));
            card.Controls.Add(head);

            card.Controls.Add(Ui.Lbl((b.AsStudent ? "Oktató: " : "Diák: ") + (b.OtherName ?? "ismeretlen"), 10.5F, Theme.DarkText));

            string when = Ui.FormatRange(b.StartTime, b.EndTime);
            if (b.Price is > 0) when += $"  ·  {Ui.Money(b.Price.Value)}";
            card.Controls.Add(Ui.Lbl(when, 10F, Ui.Muted));

            if (!string.IsNullOrWhiteSpace(b.Note))
                card.Controls.Add(Ui.Lbl("Üzenet: " + b.Note, 10F, Ui.Muted, wrap: true));

            if (b.Review != null)
            {
                int stars = Math.Clamp(b.Review.Rating, 0, 5);
                string text = "Értékelés: " + new string('★', stars) + new string('☆', 5 - stars);
                if (!string.IsNullOrWhiteSpace(b.Review.Comment)) text += "\n" + b.Review.Comment;
                card.Controls.Add(Ui.Lbl(text, 10F, Ui.Muted, wrap: true));
            }

            if (b.CanConfirm || b.CanReject || b.CanCancel)
            {
                var actions = Ui.Row();
                if (b.CanConfirm)
                    actions.Controls.Add(Ui.Btn("Elfogadás", Ui.Success, Theme.White, async (_, _) => await ActAsync(b, "confirm", null)));
                if (b.CanReject)
                    actions.Controls.Add(Ui.Btn("Elutasítás", Theme.White, Ui.Danger,
                        async (_, _) => await ActAsync(b, "reject", "Biztosan elutasítod ezt a foglalást?"), outline: true));
                if (b.CanCancel && !b.CanReject)
                    actions.Controls.Add(Ui.Btn("Lemondás", Theme.White, Ui.Danger,
                        async (_, _) => await ActAsync(b, "cancel", "Biztosan lemondod ezt a foglalást?"), outline: true));
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
            top.Controls.Add(new AvatarCircle(account.Name, 64));
            var names = new CardPanel { BackColor = Color.Transparent, Padding = new Padding(0), Margin = new Padding(0) };
            names.Controls.Add(Ui.Lbl(account.Name, 16F, Theme.DarkText, bold: true, margin: new Padding(0, 6, 0, 2)));
            names.Controls.Add(Ui.Lbl(account.Email, 10.5F, Ui.Muted, margin: new Padding(0, 0, 0, 6)));
            names.Controls.Add(Ui.Chip(Ui.RoleName(account.Role), Theme.MainBlue));
            top.Controls.Add(names);
            head.Controls.Add(top);
            AddRow(head);

            if (!string.IsNullOrWhiteSpace(account.Bio))
            {
                var about = new CardPanel();
                about.Controls.Add(Ui.Lbl("Bemutatkozás", 12F, Theme.DarkText, bold: true, margin: new Padding(0, 0, 0, 8)));
                about.Controls.Add(Ui.Lbl(account.Bio, 10.5F, Ui.Muted, wrap: true, margin: new Padding(0)));
                AddRow(about);
            }

            if (account.Role == "TUTOR")
            {
                var card = new CardPanel();
                card.Controls.Add(Ui.Lbl("Tantárgyaid és óradíjaid", 12F, Theme.DarkText, bold: true, margin: new Padding(0, 0, 0, 10)));
                if (account.Subjects == null || account.Subjects.Count == 0)
                    card.Controls.Add(Ui.Lbl("Még nem adtál meg tantárgyat. A weboldalon a fiókod szerkesztésénél pótolhatod.", 10F, Ui.Muted, wrap: true, margin: new Padding(0)));
                else
                {
                    var chips = Ui.WrapRow();
                    foreach (var subject in account.Subjects)
                        chips.Controls.Add(Ui.Chip($"{subject.Name} · {Ui.Money(subject.HourlyRate)}/óra", Theme.MainBlueLight, Theme.MainBlueDark));
                    card.Controls.Add(chips);
                }
                AddRow(card);
            }

            if (account.Role == "STUDENT")
            {
                var card = new CardPanel();
                card.Controls.Add(Ui.Lbl("Tanulmányaid", 12F, Theme.DarkText, bold: true, margin: new Padding(0, 0, 0, 10)));
                card.Controls.Add(Ui.KeyValue("Iskolatípus", string.IsNullOrWhiteSpace(account.SchoolLevel) ? "–" : account.SchoolLevel!));
                card.Controls.Add(Ui.KeyValue("Évfolyam", string.IsNullOrWhiteSpace(account.Grade) ? "–" : account.Grade!));
                if (account.LearningSubjects is { Count: > 0 })
                {
                    card.Controls.Add(Ui.Lbl("Ebben kérnél segítséget", 10F, Ui.Muted, margin: new Padding(0, 8, 0, 6)));
                    var chips = Ui.WrapRow();
                    foreach (var subject in account.LearningSubjects)
                        chips.Controls.Add(Ui.Chip(subject.Name, Theme.MainBlueLight, Theme.MainBlueDark));
                    card.Controls.Add(chips);
                }
                AddRow(card);
            }

            AddRow(Ui.Lbl("A profil szerkesztése egyelőre a weboldalon érhető el.", 9.5F, Ui.Muted));
        }
    }

    // ================================================================
    // BEÁLLÍTÁSOK
    // ================================================================
    internal class SettingsPage : PageBase
    {
        private readonly UserInfo user;

        public SettingsPage(UserInfo user) : base("Beállítások", "Az alkalmazás és a fiókod információi.")
        {
            this.user = user;
        }

        protected override Task BuildAsync()
        {
            ClearBody();

            var account = new CardPanel();
            account.Controls.Add(Ui.Lbl("Fiók", 12F, Theme.DarkText, bold: true, margin: new Padding(0, 0, 0, 10)));
            account.Controls.Add(Ui.KeyValue("Név", user.Name));
            account.Controls.Add(Ui.KeyValue("E-mail", user.Email));
            account.Controls.Add(Ui.KeyValue("Szerepkör", Ui.RoleName(user.Role)));
            AddRow(account);

            var app = new CardPanel();
            app.Controls.Add(Ui.Lbl("Alkalmazás", 12F, Theme.DarkText, bold: true, margin: new Padding(0, 0, 0, 10)));
            app.Controls.Add(Ui.KeyValue("Szerver", ApiClient.BaseUrl));
            app.Controls.Add(Ui.Lbl("A szerver címét a TUTORCONNECT_URL környezeti változóval lehet módosítani.", 9.5F, Ui.Muted, wrap: true, margin: new Padding(0, 6, 0, 0)));
            AddRow(app);

            AddRow(Ui.Lbl("További beállítások hamarosan.", 9.5F, Ui.Muted));
            return Task.CompletedTask;
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
            AddRow(Ui.MessageCard("A chat hamarosan elérhető lesz az alkalmazásban. Addig használd a weboldalon."));
            return Task.CompletedTask;
        }
    }
}

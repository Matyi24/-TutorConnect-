using System.Globalization;

namespace TutorConnectApp
{
    // ================================================================
    // PROFIL: a fiók adatai (szerepkör szerinti mezőkkel)
    // ================================================================
    internal class ProfilePage : PageBase
    {
        public ProfilePage() : base("Profil", "A fiókod adatai.") { }

        public override bool InvalidatesOthers => true;

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
            if (account.Role == "TUTOR")
                head.Controls.Add(Ui.Btn("Így látnak a diákok", Theme.CardBg, Theme.MainBlue, async (_, _) => await PreviewAsync(account), outline: true));
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

        /// <summary>Az oktató saját profilja úgy, ahogy a diákok látják (műveletek nélkül).</summary>
        private async Task PreviewAsync(AccountDto account)
        {
            try
            {
                var tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                if (IsDisposed) return;

                var tutor = tutors.FirstOrDefault(t => t.Id == account.Id);
                if (tutor == null)
                {
                    MessageBox.Show(FindForm(), "A profilod még nem jelenik meg a diákoknak. Add meg a tantárgyaidat és az óradíjaidat az adatok szerkesztésénél.",
                        "Profil előnézete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dialog = new TutorProfileDialog(tutor, preview: true);
                dialog.ShowDialog(FindForm());
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Nem sikerült megnyitni az előnézetet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task ResendAsync()
        {
            await VerifyUi.ResendAsync(FindForm());
            if (!IsDisposed) await ReloadAsync();
        }

        private async Task EditAsync(AccountDto account)
        {
            try
            {
                var subjects = await ApiClient.GetCachedAsync<List<SubjectDto>>("/api/subjects");
                if (IsDisposed) return;

                using var dialog = new EditProfileDialog(account, subjects);
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK && !IsDisposed)
                    await ReloadAsync();
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Nem sikerült megnyitni a szerkesztést", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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

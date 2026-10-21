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
            var unratedTask = LoadUnratedAsync();
            await Task.WhenAll(statsTask, summaryTask, upcomingTask, unratedTask);
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

            AddUnratedLessons(unratedTask.Result);

            AddRow(Ui.Section("Foglalásaid"));
            var mine = Ui.WrapRow();
            mine.Controls.Add(new StatTile(summary.Pending, "", tutor ? "Jóváhagyásra vár" : "Függőben", "\uE823", Theme.Warning));
            mine.Controls.Add(new StatTile(summary.Upcoming, "", "Közelgő óra", "\uE787", Theme.Success));
            mine.Controls.Add(new StatTile(summary.Past, "", "Lezajlott óra", "\uE73E", Theme.MainBlue));
            mine.Controls.Add(new StatTile(summary.Cancelled, "", "Lemondott", "\uE711", Theme.Muted));
            AddRow(mine);

            AddRow(Ui.Section("Következő óráid"));
            if (upcoming.Count == 0)
            {
                AddRow(Ui.MessageCard(
                    student ? "Nincs közelgő órád. Foglalj időpontot az Oktatók menüpontban." : "Nincs közelgő órád.",
                    "\uE787"));
            }
            else
            {
                foreach (var booking in upcoming.Take(3))
                    AddRow(UpcomingCard(booking));
            }

            AddRow(Ui.Section("A TutorConnect számokban"));
            var site = Ui.WrapRow();
            site.Controls.Add(new StatTile(stats.Tutors, "", "Oktató", "\uE716", Theme.MainBlueDark));
            site.Controls.Add(new StatTile(stats.Bookings, "", "Foglalás", "\uE787", Theme.MainBlueDark));
            site.Controls.Add(new StatTile(stats.Satisfaction, "%", "Elégedettség", "\uE734", Theme.MainBlueDark));
            AddRow(site);
        }

        /// <summary>A diák lezajlott, még nem értékelt órái (csak emlékeztető, ezért hiba esetén nincs kártya).</summary>
        private async Task<List<BookingDto>> LoadUnratedAsync()
        {
            if (user.Role != "STUDENT" || !AppSettings.ReviewPromptsEnabled) return new List<BookingDto>();

            try
            {
                var completed = await ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=past");
                return ReviewPrompts.Pending(completed, DateTime.UtcNow);
            }
            catch (ApiException)
            {
                return new List<BookingDto>();
            }
        }

        private void AddUnratedLessons(List<BookingDto> unrated)
        {
            if (unrated.Count == 0) return;

            AddRow(Ui.Section("Értékeld az óráidat"));
            foreach (var booking in unrated.Take(3))
            {
                var card = new AccentCard(Theme.Warning);

                var head = Ui.Row();
                head.Controls.Add(Ui.Lbl(booking.SubjectName ?? "Óra", 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 2, 12, 0)));
                head.Controls.Add(Ui.Chip("Értékelésre vár", Theme.Warning));
                card.Controls.Add(head);
                card.Controls.Add(Ui.Lbl("Oktató: " + (booking.OtherName ?? "ismeretlen"), 10.5F, Theme.Text));
                card.Controls.Add(Ui.Lbl(Ui.FormatRange(booking.StartTime, booking.EndTime), 10F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 8)));

                var captured = booking;
                var actions = Ui.Row();
                actions.Controls.Add(Ui.PrimaryBtn("Értékelés írása", async (_, _) => await ReviewAsync(captured)));
                card.Controls.Add(actions);
                AddRow(card);
            }

            if (unrated.Count > 3)
                AddRow(Ui.Lbl($"Még {unrated.Count - 3} óra vár értékelésre a Foglalás oldalon.", 9.5F, Theme.Muted));
        }

        private async Task ReviewAsync(BookingDto booking)
        {
            using var dialog = new ReviewDialog(booking);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

            AppEvents.RaiseBadgesStale(); // a foglalások oldal és az oktatók értékelései a következő megnyitáskor frissülnek
            await ReloadAsync();
        }

        private static CardPanel UpcomingCard(BookingDto b)
        {
            var card = new CardPanel();
            var head = Ui.Row();
            head.Controls.Add(Ui.Lbl(b.SubjectName ?? "Tantárgy", 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 2, 12, 0)));
            head.Controls.Add(Ui.Chip("Visszaigazolva", Theme.Success));
            card.Controls.Add(head);
            card.Controls.Add(Ui.Lbl((b.AsStudent ? "Oktató: " : "Diák: ") + (b.OtherName ?? "ismeretlen"), 10.5F, Theme.Text));
            card.Controls.Add(Ui.Lbl(Ui.FormatRange(b.StartTime, b.EndTime), 10F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 8)));
            card.Controls.Add(new LessonNoteRow(b));
            return card;
        }
    }
}

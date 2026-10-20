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
            mine.Controls.Add(new StatTile(summary.Pending, "", tutor ? "Jóváhagyásra vár" : "Függőben", "", Theme.Warning));
            mine.Controls.Add(new StatTile(summary.Upcoming, "", "Közelgő óra", "", Theme.Success));
            mine.Controls.Add(new StatTile(summary.Past, "", "Lezajlott óra", "", Theme.MainBlue));
            mine.Controls.Add(new StatTile(summary.Cancelled, "", "Lemondott", "", Theme.Muted));
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
            head.Controls.Add(Ui.Chip("Visszaigazolva", Theme.Success));
            card.Controls.Add(head);
            card.Controls.Add(Ui.Lbl((b.AsStudent ? "Oktató: " : "Diák: ") + (b.OtherName ?? "ismeretlen"), 10.5F, Theme.Text));
            card.Controls.Add(Ui.Lbl(Ui.FormatRange(b.StartTime, b.EndTime), 10F, Theme.Muted, margin: Ui.Pad(0)));
            return card;
        }
    }
}

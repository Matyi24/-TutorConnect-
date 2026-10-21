namespace TutorConnectApp
{
    // ================================================================
    // STATISZTIKA: óraszám és bevétel (oktató), illetve tanulási idő és költés (diák) hónapok szerint.
    // Az adatok a lezajlott és a közelgő foglalásokból, az óradíjból számolódnak (a szerver legfeljebb 200 órát ad vissza).
    // ================================================================
    internal sealed class StatsPage : PageBase
    {
        private readonly UserInfo user;
        private readonly bool tutor;
        private readonly Dictionary<int, SelectableChip> rangeChips = new();
        private FlowLayoutPanel chipRow = null!;
        private List<BookingDto> completed = new();
        private List<BookingDto> upcoming = new();
        private int months = 6;

        public StatsPage(UserInfo user) : base("Statisztika",
            user.Role == "TUTOR" ? "Az óráid és a bevételed hónapok szerint." : "A tanulásod és a költéseid hónapok szerint.")
        {
            this.user = user;
            tutor = user.Role == "TUTOR";
        }

        public override bool InvalidatesOthers => false;

        protected override async Task BuildAsync()
        {
            var completedTask = ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=past");
            var upcomingTask = ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=upcoming");
            await Task.WhenAll(completedTask, upcomingTask);
            if (IsDisposed) return;

            completed = completedTask.Result;
            upcoming = upcomingTask.Result;

            ClearBody();
            rangeChips.Clear();

            chipRow = Ui.WrapRow();
            foreach (var (value, label) in new[] { (3, "3 hónap"), (6, "6 hónap"), (12, "12 hónap") })
            {
                int range = value;
                var chip = new SelectableChip(label);
                chip.SetSelected(range == months);
                chip.Click += (_, _) =>
                {
                    if (months == range) return;
                    months = range;
                    foreach (var pair in rangeChips) pair.Value.SetSelected(pair.Key == months);
                    ShowStats();
                };
                rangeChips[range] = chip;
                chipRow.Controls.Add(chip);
            }

            var export = Ui.Btn("Exportálás (CSV)", Theme.CardBg, Theme.Text, async (_, _) => await BookingExport.ExportAllAsync(FindForm(), user.Role), outline: true);
            export.Margin = Ui.Pad(12, 0, 0, 10);
            chipRow.Controls.Add(export);
            AddRow(chipRow);

            ShowStats();
        }

        private void ShowStats()
        {
            Body.SuspendLayout();
            RemoveRowsAfter(chipRow);

            var today = DateTime.Today;
            var range = StatsLogic.InRange(completed, today, months);

            if (range.Count == 0)
            {
                AddRow(Ui.MessageCard(
                    $"Az elmúlt {months} hónapban még nem volt lezajlott órád, ezért nincs mit kimutatni." +
                    (upcoming.Count > 0 ? " A közelgő órák a lezajlásuk után kerülnek ide." : ""), "\uE9D2"));
                Body.ResumeLayout(true);
                return;
            }

            double hours = range.Sum(StatsLogic.HoursOf);
            decimal amount = range.Sum(b => b.Price ?? 0m);
            decimal upcomingAmount = upcoming.Sum(b => b.Price ?? 0m);

            // ---------- csempék ----------
            var tiles = Ui.WrapRow();
            tiles.Controls.Add(new StatTile(range.Count, "", "Lezajlott óra", "\uE81C", Theme.MainBlue));
            tiles.Controls.Add(new StatTile((int)Math.Round(hours), " óra", tutor ? "Tartott idő" : "Tanulással töltött idő", "\uE916", Theme.Success));
            tiles.Controls.Add(new StatTile((int)Math.Round(amount), " Ft", tutor ? "Bevétel" : "Költés", "\uE8C7", Theme.Warning, group: true));
            tiles.Controls.Add(tutor
                ? new StatTile((int)Math.Round(upcomingAmount), " Ft", "Várható (közelgő órák)", "\uE787", Theme.MainBlueDark, group: true)
                : new StatTile(upcoming.Count, "", "Közelgő óra", "\uE787", Theme.MainBlueDark));
            AddRow(tiles);

            // ---------- havi diagramok ----------
            var monthly = StatsLogic.Monthly(range, today, months);
            string MonthLabel(DateTime month) => month.ToString("MMM", Ui.Hu).TrimEnd('.');
            string MonthFull(DateTime month) => month.ToString("yyyy. MMMM", Ui.Hu);

            var lessonsCard = new CardPanel();
            lessonsCard.Controls.Add(Ui.Lbl("Órák száma havonta", 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 2)));
            lessonsCard.Controls.Add(Ui.Lbl(tutor ? "Ennyi órát tartottál az egyes hónapokban." : "Ennyi órád volt az egyes hónapokban.", 9.5F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 8)));
            var lessonChart = new BarChart(Theme.MainBlue);
            lessonChart.SetBars(monthly.Select(m => new BarChart.Bar(
                MonthLabel(m.Month), m.Lessons, m.Lessons.ToString(),
                $"{MonthFull(m.Month)}: {m.Lessons} óra ({StatsLogic.HoursText(m.Hours)} óra hossz)")));
            lessonsCard.Controls.Add(lessonChart);
            AddRow(lessonsCard);

            var moneyCard = new CardPanel();
            moneyCard.Controls.Add(Ui.Lbl(tutor ? "Bevétel havonta" : "Költés havonta", 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 2)));
            string average = hours > 0 ? $"Átlagos óradíj: {Ui.Money(amount / (decimal)hours)} / óra." : "";
            moneyCard.Controls.Add(Ui.Lbl(
                "A lezajlott órák árából számolva. " + average, 9.5F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 8)));
            var moneyChart = new BarChart(Theme.Success);
            moneyChart.SetBars(monthly.Select(m => new BarChart.Bar(
                MonthLabel(m.Month), (double)m.Amount, StatsLogic.ShortMoney(m.Amount),
                $"{MonthFull(m.Month)}: {Ui.Money(m.Amount)}")));
            moneyCard.Controls.Add(moneyChart);
            AddRow(moneyCard);

            // ---------- megoszlások ----------
            AddRankCard("Tantárgyak szerint", range, b => b.SubjectName, Theme.MainBlue);
            AddRankCard(tutor ? "Legtöbbet tanított diákjaid" : "Leggyakoribb oktatóid", range, b => b.OtherName, Theme.Success);

            if (completed.Count >= 200)
                AddRow(Ui.Lbl("A szerver legfeljebb a legutóbbi 200 lezajlott órát adja vissza, ezért a régebbi hónapok hiányosak lehetnek.", 9.5F, Theme.Muted, wrap: true));

            Body.ResumeLayout(true);
        }

        private void AddRankCard(string title, List<BookingDto> range, Func<BookingDto, string?> key, Color color)
        {
            var ranking = StatsLogic.Rank(range, key, 6);
            if (ranking.Count == 0) return;

            var card = new CardPanel();
            card.Controls.Add(Ui.Lbl(title, 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 8)));

            var bars = new RankBars(color);
            bars.SetRows(ranking.Select(r => new RankBars.Row(
                r.Name, r.Lessons, $"{r.Lessons} óra" + (r.Amount > 0 ? $" · {Ui.Money(r.Amount)}" : ""))));
            card.Controls.Add(bars);
            AddRow(card);
        }
    }
}

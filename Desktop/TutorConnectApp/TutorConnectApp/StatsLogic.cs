namespace TutorConnectApp
{
    /// <summary>Egy hónap összesítése a statisztikában.</summary>
    internal sealed record MonthStat(DateTime Month, int Lessons, double Hours, decimal Amount);

    /// <summary>Egy sor a rangsorban (tantárgy, diák vagy oktató szerint).</summary>
    internal sealed record RankStat(string Name, int Lessons, double Hours, decimal Amount);

    /// <summary>A statisztika számításai (felülettől független, ezért tesztelhető). A hónapok a helyi időzóna szerint értendők.</summary>
    internal static class StatsLogic
    {
        public static double HoursOf(BookingDto booking) => Math.Max((booking.EndTime - booking.StartTime).TotalHours, 0);

        /// <summary>Az időszak első napja: az aktuális hónappal együtt a megadott számú hónap.</summary>
        public static DateTime RangeStart(DateTime today, int months) =>
            new DateTime(today.Year, today.Month, 1).AddMonths(-(Math.Max(months, 1) - 1));

        public static List<BookingDto> InRange(IEnumerable<BookingDto> bookings, DateTime today, int months)
        {
            var first = RangeStart(today, months);
            return bookings.Where(b => b.StartTime.ToLocalTime() >= first).ToList();
        }

        /// <summary>Havi bontás, a legrégebbi hónap elöl; a foglalás nélküli hónapok is szerepelnek (nulla értékkel).</summary>
        public static List<MonthStat> Monthly(IEnumerable<BookingDto> bookings, DateTime today, int months)
        {
            var first = RangeStart(today, months);
            var keys = Enumerable.Range(0, Math.Max(months, 1)).Select(i => first.AddMonths(i)).ToList();
            var lessons = keys.ToDictionary(k => k, _ => 0);
            var hours = keys.ToDictionary(k => k, _ => 0.0);
            var amount = keys.ToDictionary(k => k, _ => 0m);

            foreach (var booking in bookings)
            {
                var local = booking.StartTime.ToLocalTime();
                var key = new DateTime(local.Year, local.Month, 1);
                if (!lessons.ContainsKey(key)) continue;

                lessons[key]++;
                hours[key] += HoursOf(booking);
                amount[key] += booking.Price ?? 0m;
            }

            return keys.Select(k => new MonthStat(k, lessons[k], hours[k], amount[k])).ToList();
        }

        /// <summary>Rangsor a megadott kulcs szerint (a legtöbb óra elöl), legfeljebb <paramref name="take"/> sor.</summary>
        public static List<RankStat> Rank(IEnumerable<BookingDto> bookings, Func<BookingDto, string?> key, int take)
        {
            return bookings
                .GroupBy(b => string.IsNullOrWhiteSpace(key(b)) ? "Ismeretlen" : key(b)!.Trim())
                .Select(g => new RankStat(g.Key, g.Count(), g.Sum(HoursOf), g.Sum(b => b.Price ?? 0m)))
                .OrderByDescending(r => r.Lessons)
                .ThenByDescending(r => r.Amount)
                .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(take)
                .ToList();
        }

        /// <summary>Szép, kerek felső határ a diagramhoz (1, 2, 2,5, 5 vagy 10 × tízhatvány).</summary>
        public static double NiceMax(double value)
        {
            if (value <= 0) return 1;

            double power = Math.Pow(10, Math.Floor(Math.Log10(value)));
            double fraction = value / power;
            double nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 2.5 ? 2.5 : fraction <= 5 ? 5 : 10;
            return nice * power;
        }

        /// <summary>Rövid pénzösszeg a diagram oszlopai fölé ("45 e", "1,2 M"), a kis összegek egészben.</summary>
        public static string ShortMoney(decimal amount)
        {
            var hu = System.Globalization.CultureInfo.GetCultureInfo("hu-HU");
            if (amount >= 1_000_000m) return (amount / 1_000_000m).ToString("0.#", hu) + " M";
            if (amount >= 1_000m) return (amount / 1_000m).ToString("0.#", hu) + " e";
            return amount.ToString("0", hu);
        }

        /// <summary>Az órák száma olvashatóan: egész, vagy egy tizedessel ("3", "4,5").</summary>
        public static string HoursText(double hours) =>
            Math.Round(hours, 1).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("hu-HU"));
    }
}

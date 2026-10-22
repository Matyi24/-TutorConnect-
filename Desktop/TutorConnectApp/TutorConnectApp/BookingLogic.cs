using System.Globalization;

namespace TutorConnectApp
{
    /// <summary>A foglalási kártyák szövegei (felülettől független, ezért tesztelhető).</summary>
    internal static class BookingText
    {
        private static readonly CultureInfo Hu = CultureInfo.GetCultureInfo("hu-HU");

        /// <summary>Az óra hossza olvashatóan: "45 perc", "1 óra", "1,5 óra", "2 óra 15 perc".</summary>
        public static string Duration(TimeSpan length)
        {
            int minutes = (int)Math.Round(length.TotalMinutes);
            if (minutes <= 0) return "";
            if (minutes < 60) return $"{minutes} perc";

            int hours = minutes / 60;
            int rest = minutes % 60;
            if (rest == 0) return $"{hours} óra";
            if (rest == 30) return $"{hours},5 óra";
            return $"{hours} óra {rest} perc";
        }

        /// <summary>Mennyi van hátra az óra kezdetéig: "most", "17 perc múlva", "ma 15:00", "holnap 15:00", "3 nap múlva"; múltbeli órára üres.</summary>
        public static string Relative(DateTime startUtc, DateTime nowUtc)
        {
            var start = startUtc.ToLocalTime();
            var now = nowUtc.ToLocalTime();
            var span = start - now;
            if (span < TimeSpan.Zero) return "";

            if (span < TimeSpan.FromMinutes(1)) return "most";
            if (span < TimeSpan.FromHours(1)) return $"{(int)Math.Ceiling(span.TotalMinutes)} perc múlva";
            if (start.Date == now.Date) return "ma " + start.ToString("HH:mm", Hu);
            if (start.Date == now.Date.AddDays(1)) return "holnap " + start.ToString("HH:mm", Hu);

            int days = (start.Date - now.Date).Days;
            return days <= 14 ? $"{days} nap múlva" : "";
        }

        /// <summary>A hónap rövidítése nagybetűvel, pont nélkül ("OKT").</summary>
        public static string MonthShort(DateTime local) => local.ToString("MMM", Hu).TrimEnd('.').ToUpper(Hu);
    }
}

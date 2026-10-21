using System.Globalization;
using System.Text;

namespace TutorConnectApp
{
    /// <summary>A foglalási előzmények CSV-ben: Excelben kettős kattintásra magyar beállítással nyílik (pontosvessző, UTF-8 BOM-mal).</summary>
    internal static class CsvExport
    {
        private static readonly CultureInfo Hu = CultureInfo.GetCultureInfo("hu-HU");

        public static string StatusName(string status) => status switch
        {
            "PENDING" => "Függőben",
            "CONFIRMED" => "Visszaigazolva",
            "COMPLETED" => "Lezajlott",
            "REJECTED" => "Elutasítva",
            "CANCELLED" => "Lemondva",
            _ => status
        };

        /// <param name="role">A felhasználó szerepköre ("TUTOR" vagy "STUDENT"): a "Partner" oszlop neve ettől függ.</param>
        public static string Build(IEnumerable<BookingDto> bookings, string role)
        {
            var text = new StringBuilder();
            text.Append(string.Join(';', new[]
            {
                "Dátum", "Kezdés", "Befejezés", "Időtartam (óra)", "Tantárgy",
                role == "TUTOR" ? "Diák" : "Oktató", "Állapot", "Ár (Ft)", "Megjegyzés", "Értékelés (1-5)", "Értékelés szövege"
            })).Append("\r\n");

            foreach (var b in bookings.OrderBy(b => b.StartTime))
            {
                var start = b.StartTime.ToLocalTime();
                var end = b.EndTime.ToLocalTime();

                text.Append(string.Join(';', new[]
                {
                    start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    start.ToString("HH:mm", CultureInfo.InvariantCulture),
                    end.ToString("HH:mm", CultureInfo.InvariantCulture),
                    StatsLogic.HoursOf(b).ToString("0.##", Hu),
                    Field(b.SubjectName),
                    Field(b.OtherName),
                    StatusName(b.Status),
                    b.Price is decimal price ? price.ToString("0.##", Hu) : "",
                    Field(b.Note),
                    b.Review != null ? b.Review.Rating.ToString(CultureInfo.InvariantCulture) : "",
                    Field(b.Review?.Comment)
                })).Append("\r\n");
            }
            return text.ToString();
        }

        /// <summary>
        /// Egy szöveges mező CSV-ben: idézőjelezve, ha kell, és a képletként értelmezhető kezdetek (=, +, -, @) védve,
        /// hogy egy másik felhasználó által megadott szöveg Excelben ne futhasson le képletként.
        /// </summary>
        public static string Field(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            string text = value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (text.Length > 0 && "=+-@\t".Contains(text[0])) text = "'" + text;

            bool quote = text.IndexOfAny(new[] { ';', '"', '\n' }) >= 0;
            return quote ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }
    }

    /// <summary>Egy sor a nyomtatható órarendben.</summary>
    internal sealed record TimetableEntry(string Time, string Subject, string Partner, string Status, bool Confirmed);

    internal sealed record TimetableDay(DateTime Date, List<TimetableEntry> Entries);

    /// <summary>A nyomtatható órarend adatai: a mai naptól kezdve napok szerint csoportosítva (felülettől független).</summary>
    internal static class TimetableLogic
    {
        public static List<TimetableDay> Build(IEnumerable<BookingDto> bookings, DateTime today)
        {
            return bookings
                .Where(b => b.Status is "CONFIRMED" or "PENDING" && b.EndTime.ToLocalTime() >= today)
                .OrderBy(b => b.StartTime)
                .GroupBy(b => b.StartTime.ToLocalTime().Date)
                .Select(g => new TimetableDay(g.Key, g.Select(b => new TimetableEntry(
                    $"{b.StartTime.ToLocalTime():HH:mm}–{b.EndTime.ToLocalTime():HH:mm}",
                    string.IsNullOrWhiteSpace(b.SubjectName) ? "Óra" : b.SubjectName!,
                    b.OtherName ?? "",
                    CsvExport.StatusName(b.Status),
                    b.Status == "CONFIRMED")).ToList()))
                .ToList();
        }
    }
}

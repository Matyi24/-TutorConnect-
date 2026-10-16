using System.Globalization;
using System.Text;

namespace TutorConnectApp
{
    /// <summary>A foglalások exportálása iCalendar (.ics) fájlba, amit a Google Naptár, az Outlook és a telefonok is beolvasnak.</summary>
    internal static class IcsExport
    {
        public static string Build(IEnumerable<BookingDto> bookings, bool asTutor)
        {
            var lines = new List<string>
            {
                "BEGIN:VCALENDAR",
                "VERSION:2.0",
                "PRODID:-//TutorConnect//Desktop//HU",
                "CALSCALE:GREGORIAN",
                "METHOD:PUBLISH",
                "X-WR-CALNAME:TutorConnect"
            };

            string stamp = Utc(DateTime.UtcNow);
            foreach (var booking in bookings)
            {
                string subject = string.IsNullOrWhiteSpace(booking.SubjectName) ? "Óra" : booking.SubjectName!;
                string other = booking.OtherName ?? "";
                string role = asTutor ? "diák" : "oktató";
                bool confirmed = booking.Status == "CONFIRMED";

                var description = new StringBuilder();
                description.Append(confirmed ? "Állapot: visszaigazolva" : "Állapot: függőben (az oktató még nem igazolta vissza)");
                if (!string.IsNullOrWhiteSpace(booking.Note)) description.Append("\n").Append(booking.Note!.Trim());

                lines.Add("BEGIN:VEVENT");
                lines.Add($"UID:tutorconnect-booking-{booking.Id}@tutorconnect");
                lines.Add($"DTSTAMP:{stamp}");
                lines.Add($"DTSTART:{Utc(booking.StartTime)}");
                lines.Add($"DTEND:{Utc(booking.EndTime)}");
                lines.Add("SUMMARY:" + Escape(other.Length > 0 ? $"{subject} – {other} ({role})" : subject));
                lines.Add("DESCRIPTION:" + Escape(description.ToString()));
                lines.Add("STATUS:" + (confirmed ? "CONFIRMED" : "TENTATIVE"));
                lines.Add("END:VEVENT");
            }
            lines.Add("END:VCALENDAR");

            var result = new StringBuilder();
            foreach (string line in lines)
                result.Append(Fold(line)).Append("\r\n");
            return result.ToString();
        }

        private static string Utc(DateTime value)
        {
            var utc = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
            return utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>Az iCalendar szövegmezők speciális karaktereinek védelme (RFC 5545).</summary>
        private static string Escape(string text) => text
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "");

        /// <summary>A hosszú sorokat 75 bájtonként töri (a folytatósor szóközzel kezdődik), UTF-8 karaktereket nem vág el.</summary>
        private static string Fold(string line)
        {
            if (Encoding.UTF8.GetByteCount(line) <= 75) return line;

            var result = new StringBuilder();
            int bytes = 0;
            int limit = 75;
            foreach (var rune in line.EnumerateRunes())
            {
                int size = rune.Utf8SequenceLength;
                if (bytes + size > limit)
                {
                    result.Append("\r\n ");
                    bytes = 1; // a folytatósor elején lévő szóköz
                    limit = 75;
                }
                result.Append(rune.ToString());
                bytes += size;
            }
            return result.ToString();
        }
    }
}

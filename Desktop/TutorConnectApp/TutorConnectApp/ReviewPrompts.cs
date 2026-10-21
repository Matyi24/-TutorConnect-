using System.Text.Json;

namespace TutorConnectApp
{
    /// <summary>
    /// "Értékeld az órát": a diák a lezajlott, még nem értékelt óráiról értesítést és a főoldalon emlékeztetőt kap.
    /// Az értesítés óránként egyszer, felhasználónként a gépen megjegyezve (%AppData%\TutorConnect\review-prompts-azonosító.json) jön,
    /// így újraindítás után sem ismétlődik.
    /// </summary>
    internal static class ReviewPrompts
    {
        /// <summary>Ennyi napig kérjük az értékelést a lezajlott óra után (utána már nem zaklatjuk vele a diákot).</summary>
        public const int WindowDays = 14;

        /// <summary>A diák értékelhető, de még nem értékelt órái, a legfrissebb elöl.</summary>
        public static List<BookingDto> Pending(IEnumerable<BookingDto> completed, DateTime nowUtc) => completed
            .Where(b => b.AsStudent && b.Status == "COMPLETED" && b.Review == null
                        && b.EndTime.ToUniversalTime() >= nowUtc.AddDays(-WindowDays)
                        && b.EndTime.ToUniversalTime() <= nowUtc)
            .OrderByDescending(b => b.EndTime)
            .ToList();

        // ------------------------------------------------------------
        // Már értesített órák (helyi tárolás)
        // ------------------------------------------------------------

        private static readonly HashSet<int> Notified = new();
        private static int loadedFor = -1;

        private static string FilePath(int userId) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TutorConnect", $"review-prompts-{userId}.json");

        private static void EnsureLoaded()
        {
            int userId = ApiClient.CurrentUser?.Id ?? -1;
            if (userId == loadedFor) return;

            loadedFor = userId;
            Notified.Clear();
            if (userId < 0) return;

            try
            {
                string path = FilePath(userId);
                if (File.Exists(path))
                    foreach (int id in JsonSerializer.Deserialize<List<int>>(File.ReadAllText(path)) ?? new List<int>())
                        Notified.Add(id);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                Notified.Clear();
            }
        }

        /// <summary>Azok az órák, amelyekről még nem küldtünk értesítést.</summary>
        public static List<BookingDto> NotNotified(IEnumerable<BookingDto> pending)
        {
            EnsureLoaded();
            return pending.Where(b => !Notified.Contains(b.Id)).ToList();
        }

        public static void MarkNotified(IEnumerable<BookingDto> bookings)
        {
            EnsureLoaded();
            if (loadedFor < 0) return;

            bool changed = false;
            foreach (var booking in bookings)
                changed |= Notified.Add(booking.Id);
            if (!changed) return;

            try
            {
                string path = FilePath(loadedFor);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(Notified.ToList()));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // nem kritikus: legfeljebb újraindítás után még egyszer jelez
            }
        }
    }
}

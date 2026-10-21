using System.Text.Json;

namespace TutorConnectApp
{
    /// <summary>
    /// Emlékeztetők a visszaigazolt órákra: 1 órával és 10 perccel az óra előtt értesít az app (amíg fut).
    /// A már elküldött emlékeztetőket felhasználónként a gépen jegyzi (%AppData%\TutorConnect\reminders-azonosító.json),
    /// így újraindítás után sem jön ugyanaz kétszer. Ha az app az óra előtt 40 perccel indul, az 1 órás emlékeztető
    /// már nem jön, csak a 10 perces.
    /// </summary>
    internal sealed class LessonReminders
    {
        private const int RefreshMinutes = 10;

        private List<BookingDto> upcoming = new();
        private readonly HashSet<string> sent = new();
        private int loadedFor = -1;
        private long lastRefresh;

        private static string FilePath(int userId) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TutorConnect", $"reminders-{userId}.json");

        /// <summary>Igaz, ha rég töltöttük le a közelgő órákat (vagy változás történt).</summary>
        public bool NeedsRefresh => Environment.TickCount64 - lastRefresh > RefreshMinutes * 60_000L;

        /// <summary>Foglalás-változás után: a következő ellenőrzéskor újra letölti a közelgő órákat.</summary>
        public void MarkStale() => lastRefresh = 0;

        private void EnsureLoaded()
        {
            int userId = ApiClient.CurrentUser?.Id ?? -1;
            if (userId == loadedFor) return;

            loadedFor = userId;
            sent.Clear();
            upcoming = new List<BookingDto>();
            lastRefresh = 0;
            if (userId < 0) return;

            try
            {
                string path = FilePath(userId);
                if (File.Exists(path))
                    foreach (string key in JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? new List<string>())
                        sent.Add(key);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                sent.Clear();
            }
        }

        private void Save()
        {
            if (loadedFor < 0) return;

            try
            {
                string path = FilePath(loadedFor);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(sent.ToList()));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // nem kritikus: legfeljebb újraindítás után még egyszer jelez
            }
        }

        private static int IdOf(string key)
        {
            int colon = key.IndexOf(':');
            return colon > 0 && int.TryParse(key[..colon], out int id) ? id : -1;
        }

        /// <summary>Letölti a visszaigazolt közelgő órákat, és elfelejti a lezárult órák emlékeztetőit.</summary>
        public async Task RefreshAsync()
        {
            EnsureLoaded();

            var list = await ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=upcoming");
            upcoming = list.Where(b => b.Status == "CONFIRMED").ToList();
            lastRefresh = Environment.TickCount64;

            var ids = upcoming.Select(b => b.Id).ToHashSet();
            if (sent.RemoveWhere(key => !ids.Contains(IdOf(key))) > 0)
                Save();
        }

        /// <summary>Az éppen esedékes emlékeztetők (percenként hívandó). Amit visszaad, azt elküldöttnek jegyzi.</summary>
        public List<(BookingDto Booking, int Minutes, bool Final)> Due()
        {
            EnsureLoaded();

            var result = new List<(BookingDto, int, bool)>();
            var now = DateTime.UtcNow;
            foreach (var booking in upcoming)
            {
                double minutes = (booking.StartTime.ToUniversalTime() - now).TotalMinutes;
                if (minutes <= 0) continue; // már elkezdődött

                string hourKey = $"{booking.Id}:60";
                string finalKey = $"{booking.Id}:10";

                if (minutes <= 10)
                {
                    if (sent.Add(finalKey))
                    {
                        sent.Add(hourKey); // az 1 órás már nem kell
                        result.Add((booking, (int)Math.Ceiling(minutes), true));
                    }
                }
                else if (minutes <= 60 && sent.Add(hourKey))
                {
                    result.Add((booking, (int)Math.Ceiling(minutes), false));
                }
            }

            if (result.Count > 0) Save();
            return result;
        }
    }
}

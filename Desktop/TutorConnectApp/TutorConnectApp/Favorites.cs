using System.Text.Json;

namespace TutorConnectApp
{
    /// <summary>
    /// A diák kedvenc oktatói. Felhasználónként külön fájlban tárolódnak a gépen
    /// (%AppData%\TutorConnect\favorites-azonosító.json), a szerver nem tud róluk.
    /// Mellettük az utoljára látott szabad időpontok is itt vannak: ebből derül ki, ha egy kedvenc új időpontot hirdetett.
    /// </summary>
    internal static class Favorites
    {
        private sealed class Data
        {
            public List<int> Ids { get; set; } = new();

            /// <summary>Oktató azonosítója → az utoljára látott szabad időpontjainak azonosítói.</summary>
            public Dictionary<string, List<int>> Known { get; set; } = new();
        }

        private static Data data = new();
        private static int loadedFor = -1;

        private static string FilePath(int userId) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TutorConnect", $"favorites-{userId}.json");

        /// <summary>Betölti az aktuális felhasználó adatait (felhasználóváltáskor újra).</summary>
        private static void EnsureLoaded()
        {
            int userId = ApiClient.CurrentUser?.Id ?? -1;
            if (userId == loadedFor) return;

            loadedFor = userId;
            data = new Data();
            if (userId < 0) return;

            try
            {
                string path = FilePath(userId);
                if (File.Exists(path))
                    data = JsonSerializer.Deserialize<Data>(File.ReadAllText(path)) ?? new Data();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                data = new Data(); // sérült vagy nem olvasható fájl: üres lista
            }
        }

        private static void Save()
        {
            if (loadedFor < 0) return;

            try
            {
                string path = FilePath(loadedFor);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(data));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // a mentés nem kritikus
            }
        }

        public static IReadOnlyList<int> Ids
        {
            get
            {
                EnsureLoaded();
                return data.Ids;
            }
        }

        public static bool IsFavorite(int tutorId)
        {
            EnsureLoaded();
            return data.Ids.Contains(tutorId);
        }

        /// <summary>Hozzáadja vagy eltávolítja a kedvencek közül. Igaz, ha utána kedvenc.</summary>
        public static bool Toggle(int tutorId)
        {
            EnsureLoaded();

            bool nowFavorite;
            if (data.Ids.Remove(tutorId))
            {
                data.Known.Remove(tutorId.ToString());
                nowFavorite = false;
            }
            else
            {
                data.Ids.Add(tutorId);
                nowFavorite = true;
            }

            Save();
            return nowFavorite;
        }

        /// <summary>Az utoljára látott szabad időpontok; null, ha még nem néztük (az első ellenőrzés csak alapállapotot rögzít).</summary>
        public static List<int>? GetKnown(int tutorId)
        {
            EnsureLoaded();
            return data.Known.TryGetValue(tutorId.ToString(), out var slots) ? slots : null;
        }

        public static void SetKnown(int tutorId, List<int> slotIds)
        {
            EnsureLoaded();
            data.Known[tutorId.ToString()] = slotIds;
            Save();
        }
    }
}

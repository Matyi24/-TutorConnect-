using System.Net;
using System.Text.Json;

namespace TutorConnectApp
{
    /// <summary>A bejelentkezett felhasználó adatai (a szerver /api/me válaszából).</summary>
    internal record UserInfo(int Id, string Name, string Email, string Role);

    /// <summary>
    /// Kapcsolat a TutorConnect szerverrel (vsc/js/main.js). A bejelentkezést a szerver végzi
    /// (argon2 ellenőrzés), az app a session sütit tartja meg, így nincs adatbázis-jelszó az appban.
    /// A szerver címe: TUTORCONNECT_URL környezeti változó, alapértelmezetten http://localhost:3000
    /// </summary>
    internal static class ApiClient
    {
        public static readonly string BaseUrl =
            (Environment.GetEnvironmentVariable("TUTORCONNECT_URL") ?? "http://localhost:3000").TrimEnd('/');

        private static readonly HttpClient http = new(new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            AllowAutoRedirect = false // a szerver sikeres belépésnél /index-re irányít, ezt nem követjük
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public static UserInfo? CurrentUser { get; private set; }

        /// <summary>Bejelentkezés. Siker esetén (true, null), egyébként (false, hibaüzenet).</summary>
        public static async Task<(bool Ok, string? Error)> LoginAsync(string email, string password)
        {
            try
            {
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["email"] = email,
                    ["password"] = password
                });

                using var response = await http.PostAsync($"{BaseUrl}/login", form);

                if (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.Found)
                {
                    var user = await FetchMeAsync();
                    if (user == null)
                        return (false, "A bejelentkezés nem sikerült, próbáld újra.");

                    CurrentUser = user;
                    return (true, null);
                }

                string message = (await response.Content.ReadAsStringAsync()).Trim();
                return (false, message.Length > 0 && message.Length < 200 ? message : "Hibás e-mail vagy jelszó.");
            }
            catch (TaskCanceledException)
            {
                return (false, "A szerver nem válaszol (időtúllépés).");
            }
            catch (HttpRequestException)
            {
                return (false, $"A szerver nem érhető el: {BaseUrl}\nIndítsd el a szervert (npm run dev).");
            }
        }

        public static async Task LogoutAsync()
        {
            try
            {
                using var response = await http.PostAsync($"{BaseUrl}/logout", null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Ha a szerver nem érhető el, a helyi állapotot akkor is töröljük.
            }
            CurrentUser = null;
        }

        private static async Task<UserInfo?> FetchMeAsync()
        {
            using var response = await http.GetAsync($"{BaseUrl}/api/me");
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            if (!root.TryGetProperty("loggedIn", out var loggedIn) || !loggedIn.GetBoolean()) return null;
            var user = root.GetProperty("user");

            return new UserInfo(
                user.GetProperty("id").GetInt32(),
                user.GetProperty("name").GetString() ?? "",
                user.GetProperty("email").GetString() ?? "",
                user.GetProperty("role").GetString() ?? "");
        }
    }
}

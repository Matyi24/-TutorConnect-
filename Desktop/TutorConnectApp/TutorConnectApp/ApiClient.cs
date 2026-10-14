using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TutorConnectApp
{
    /// <summary>A szerverrel való kommunikáció hibája, a szöveg közvetlenül megjeleníthető.</summary>
    internal class ApiException : Exception
    {
        public ApiException(string message) : base(message) { }
    }

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
                bool plainText = message.Length > 0 && message.Length < 200 && !message.StartsWith("<");

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.BadRequest)
                    return (false, plainText ? message : "Hibás e-mail vagy jelszó.");

                // Minden más válasz szerver- vagy címhiba: kiírjuk a kódot, hogy kiderüljön az ok.
                return (false, $"Váratlan szerverválasz (HTTP {(int)response.StatusCode}) ezen a címen: {BaseUrl}" +
                               (plainText ? $"\n{message}" : ""));
            }
            catch (TaskCanceledException)
            {
                return (false, "A szerver nem válaszol (időtúllépés).");
            }
            catch (HttpRequestException)
            {
                return (false, $"A szerver nem érhető el: {BaseUrl}\nIndítsd el a szervert (npm run dev), és ellenőrizd, hogy fut a MySQL.");
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                return (false, $"Hibás válasz érkezett a szervertől: {ex.Message}");
            }
        }

        // ------------------------------------------------------------
        // Általános lekérdezések (a bejelentkezett session sütivel)
        // ------------------------------------------------------------

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            // a MySQL a decimal értékeket (ár, átlag) szövegként adja vissza
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public static async Task<T> GetAsync<T>(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
            string body = await SendAsync(request);
            try
            {
                return JsonSerializer.Deserialize<T>(body, JsonOptions) ?? throw new ApiException("Üres válasz érkezett a szervertől.");
            }
            catch (JsonException ex)
            {
                throw new ApiException("Hibás szerverválasz: " + ex.Message);
            }
        }

        public static async Task PostAsync(string path, object? payload = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path);
            if (payload != null)
                request.Content = JsonContent.Create(payload);
            await SendAsync(request);
        }

        private static async Task<string> SendAsync(HttpRequestMessage request)
        {
            try
            {
                using var response = await http.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new ApiException("A munkamenet lejárt. Jelentkezz ki, majd be újra.");
                if (!response.IsSuccessStatusCode)
                    throw new ApiException(ReadError(body, response.StatusCode));

                return body;
            }
            catch (TaskCanceledException)
            {
                throw new ApiException("A szerver nem válaszol (időtúllépés).");
            }
            catch (HttpRequestException)
            {
                throw new ApiException($"A szerver nem érhető el: {BaseUrl}");
            }
        }

        private static string ReadError(string body, HttpStatusCode status)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("error", out var error)
                    && error.GetString() is { Length: > 0 } message)
                    return message;
            }
            catch (JsonException)
            {
                // nem JSON a válasz, marad az általános üzenet
            }
            return $"Szerverhiba (HTTP {(int)status}).";
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

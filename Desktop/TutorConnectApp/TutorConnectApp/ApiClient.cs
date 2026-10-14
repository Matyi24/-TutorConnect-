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

        private static readonly HttpClient Http = new(new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            AllowAutoRedirect = false // a szerver sikeres belépésnél /index-re irányít, ezt nem követjük
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            // a MySQL a decimal értékeket (ár, átlag) szövegként adja vissza
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private const string TimeoutMessage = "A szerver nem válaszol (időtúllépés).";

        private static string UnreachableMessage => $"A szerver nem érhető el: {BaseUrl}";

        public static UserInfo? CurrentUser { get; private set; }

        // ------------------------------------------------------------
        // Bejelentkezés / kijelentkezés
        // ------------------------------------------------------------

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

                using var response = await Http.PostAsync($"{BaseUrl}/login", form);

                // siker: a szerver átirányít a főoldalra, a session süti ilyenkor már megvan
                if (response.StatusCode == HttpStatusCode.Found)
                {
                    var me = await GetAsync<MeResponse>("/api/me");
                    if (!me.LoggedIn || me.User == null)
                        return (false, "A bejelentkezés nem sikerült, próbáld újra.");

                    CurrentUser = new UserInfo(me.User.Id, me.User.Name, me.User.Email, me.User.Role);
                    return (true, null);
                }

                string message = (await response.Content.ReadAsStringAsync()).Trim();
                bool plainText = message.Length is > 0 and < 200 && !message.StartsWith('<');

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                    return (false, plainText ? message : "Hibás e-mail vagy jelszó.");

                // minden más válasz szerver- vagy címhiba: kiírjuk a kódot, hogy kiderüljön az ok
                return (false, $"Váratlan szerverválasz (HTTP {(int)response.StatusCode}) ezen a címen: {BaseUrl}" +
                               (plainText ? $"\n{message}" : ""));
            }
            catch (ApiException ex)
            {
                return (false, ex.Message);
            }
            catch (TaskCanceledException)
            {
                return (false, TimeoutMessage);
            }
            catch (HttpRequestException)
            {
                return (false, UnreachableMessage + "\nIndítsd el a szervert (npm run dev), és ellenőrizd, hogy fut a MySQL.");
            }
        }

        public static async Task LogoutAsync()
        {
            try
            {
                using var response = await Http.PostAsync($"{BaseUrl}/logout", null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // ha a szerver nem érhető el, a helyi állapotot akkor is töröljük
            }
            CurrentUser = null;
        }

        // ------------------------------------------------------------
        // Általános kérések (a bejelentkezett session sütivel)
        // ------------------------------------------------------------

        public static async Task<T> GetAsync<T>(string path) =>
            Deserialize<T>(await SendAsync(HttpMethod.Get, path, null));

        public static Task PostAsync(string path, object? payload = null) =>
            SendAsync(HttpMethod.Post, path, payload);

        /// <summary>POST, a válasz JSON-ját típusként visszaadja.</summary>
        public static async Task<T> PostAsync<T>(string path, object? payload = null) =>
            Deserialize<T>(await SendAsync(HttpMethod.Post, path, payload));

        public static Task DeleteAsync(string path) =>
            SendAsync(HttpMethod.Delete, path, null);

        private static async Task<string> SendAsync(HttpMethod method, string path, object? payload)
        {
            using var request = new HttpRequestMessage(method, BaseUrl + path);
            if (payload != null)
                request.Content = JsonContent.Create(payload);

            try
            {
                using var response = await Http.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new ApiException("A munkamenet lejárt. Jelentkezz ki, majd be újra.");
                if (!response.IsSuccessStatusCode)
                    throw new ApiException(ReadError(body, response.StatusCode));

                return body;
            }
            catch (TaskCanceledException)
            {
                throw new ApiException(TimeoutMessage);
            }
            catch (HttpRequestException)
            {
                throw new ApiException(UnreachableMessage);
            }
        }

        private static T Deserialize<T>(string body)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(body, JsonOptions)
                       ?? throw new ApiException("Üres válasz érkezett a szervertől.");
            }
            catch (JsonException ex)
            {
                throw new ApiException("Hibás szerverválasz: " + ex.Message);
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

        // a /api/me válasza
        private sealed class MeResponse
        {
            [JsonPropertyName("loggedIn")] public bool LoggedIn { get; set; }
            [JsonPropertyName("user")] public MeUser? User { get; set; }
        }

        private sealed class MeUser
        {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; } = "";
            [JsonPropertyName("email")] public string Email { get; set; } = "";
            [JsonPropertyName("role")] public string Role { get; set; } = "";
        }
    }
}

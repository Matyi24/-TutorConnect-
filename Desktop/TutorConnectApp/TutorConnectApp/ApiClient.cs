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
    internal record UserInfo(int Id, string Name, string Email, string Role, bool EmailVerified = true);

    /// <summary>
    /// Kapcsolat a TutorConnect szerverrel (vsc/js/main.js). A bejelentkezést a szerver végzi
    /// (argon2 ellenőrzés), az app a session sütit tartja meg, így nincs adatbázis-jelszó az appban.
    /// A szerver címe: TUTORCONNECT_URL környezeti változó, alapértelmezetten http://localhost:3000
    /// </summary>
    internal static class ApiClient
    {
        public static readonly string BaseUrl =
            (Environment.GetEnvironmentVariable("TUTORCONNECT_URL") ?? "http://localhost:3000").TrimEnd('/');

        private static readonly CookieContainer CookieJar = new();

        private static readonly HttpClientHandler Handler = new()
        {
            CookieContainer = CookieJar,
            AllowAutoRedirect = false // a szerver sikeres belépésnél /index-re irányít, ezt nem követjük
        };

        private static readonly HttpClient Http = new(Handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // hosszabb időkorlát a fájlok fel- és letöltéséhez (ugyanazzal a munkamenet-sütivel)
        private static readonly HttpClient FileHttp = new(Handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        /// <summary>A munkamenet sütije (a valós idejű WebSocket-kapcsolat is ezt használja).</summary>
        public static CookieContainer Cookies => CookieJar;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            // a MySQL a decimal értékeket (ár, átlag) szövegként adja vissza
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private const int CacheLifetimeMs = 30_000;

        private static readonly Dictionary<string, (long Time, string Body, object? Parsed)> Cache = new();

        private const string TimeoutMessage = "A szerver nem válaszol (időtúllépés).";

        private static string UnreachableMessage => $"A szerver nem érhető el: {BaseUrl}";

        public static UserInfo? CurrentUser { get; private set; }

        /// <summary>Akkor fut le, ha a bejelentkezett felhasználó adatai megváltoztak (pl. profil mentése után).</summary>
        public static event Action? CurrentUserChanged;

        /// <summary>Akkor fut le (egyszer), ha a szerver szerint a munkamenet lejárt: az appnak vissza kell lépnie a belépéshez.</summary>
        public static event Action? SessionExpired;

        private const string SessionCookieName = "connect.sid";
        private static bool expiredRaised;

        // ------------------------------------------------------------
        // Bejelentkezés / kijelentkezés
        // ------------------------------------------------------------

        /// <summary>Bejelentkezés. Siker esetén (true, null), egyébként (false, hibaüzenet).</summary>
        public static async Task<(bool Ok, string? Error)> LoginAsync(string email, string password, bool remember = false)
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

                    CurrentUser = new UserInfo(me.User.Id, me.User.Name, me.User.Email, me.User.Role, me.User.EmailVerified);
                    expiredRaised = false;
                    RememberSession(remember);
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
            DropSession();
        }

        /// <summary>Helyi állapot törlése (kijelentkezés vagy lejárt munkamenet után): felhasználó, gyorsítótár, mentett belépés.</summary>
        public static void DropSession()
        {
            CurrentUser = null;
            Cache.Clear();
            SessionStore.Clear();
            expiredRaised = false;
        }

        /// <summary>"Emlékezz rám": a munkamenet-sütit titkosítva a gépen tartja (vagy törli, ha remember = false).</summary>
        public static void RememberSession(bool remember)
        {
            if (!remember)
            {
                SessionStore.Clear();
                return;
            }

            var cookie = CookieJar.GetCookies(new Uri(BaseUrl))[SessionCookieName];
            if (cookie != null) SessionStore.Save(BaseUrl, cookie.Name, cookie.Value);
        }

        /// <summary>Induláskor: a mentett munkamenettel próbál belépni. Igaz, ha sikerült (a CurrentUser be van állítva).</summary>
        public static async Task<bool> TryRestoreSessionAsync()
        {
            var saved = SessionStore.Load(BaseUrl);
            if (saved == null) return false;

            CookieJar.Add(new Uri(BaseUrl), new Cookie(saved.Value.Name, saved.Value.Value));
            try
            {
                var me = await GetAsync<MeResponse>("/api/me");
                if (me.LoggedIn && me.User != null)
                {
                    CurrentUser = new UserInfo(me.User.Id, me.User.Name, me.User.Email, me.User.Role, me.User.EmailVerified);
                    expiredRaised = false;
                    return true;
                }
            }
            catch (ApiException)
            {
                return false; // a szerver nem érhető el: a mentett belépés megmarad a következő indításra
            }

            SessionStore.Clear(); // a szerver szerint lejárt
            return false;
        }

        /// <summary>A munkamenet lejárt (a szerver 401-et adott, vagy a valós idejű kapcsolat elutasítva). Csak egyszer jelez.</summary>
        public static void NotifySessionExpired()
        {
            if (CurrentUser == null || expiredRaised) return;

            expiredRaised = true;
            SessionStore.Clear();
            SessionExpired?.Invoke();
        }

        // ------------------------------------------------------------
        // Regisztráció, elfelejtett jelszó, fióktörlés
        // ------------------------------------------------------------

        /// <summary>
        /// Regisztráció (a szerver /register végpontja). Siker esetén a szerver be is léptet; ha nem, a felhasználónak
        /// kell bejelentkeznie. Visszatérés: (sikerült, be van-e léptetve, hibaüzenet).
        /// </summary>
        public static async Task<(bool Ok, bool LoggedIn, string? Error)> RegisterAsync(string name, string email, string password, string role)
        {
            try
            {
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["name"] = name,
                    ["email"] = email,
                    ["password"] = password,
                    ["password_confirm"] = password,
                    ["role"] = role
                });

                using var response = await Http.PostAsync($"{BaseUrl}/register", form);

                if (response.StatusCode == HttpStatusCode.Found)
                {
                    string target = response.Headers.Location?.ToString() ?? "";
                    if (target.Contains("/login", StringComparison.OrdinalIgnoreCase))
                        return (true, false, null); // a fiók létrejött, de az automatikus belépés nem sikerült

                    var me = await GetAsync<MeResponse>("/api/me");
                    if (!me.LoggedIn || me.User == null) return (true, false, null);

                    CurrentUser = new UserInfo(me.User.Id, me.User.Name, me.User.Email, me.User.Role, me.User.EmailVerified);
                    expiredRaised = false;
                    return (true, true, null);
                }

                string message = (await response.Content.ReadAsStringAsync()).Trim();
                bool plainText = message.Length is > 0 and < 200 && !message.StartsWith('<');
                return (false, false, plainText ? message : $"Váratlan szerverválasz (HTTP {(int)response.StatusCode}).");
            }
            catch (ApiException ex)
            {
                return (false, false, ex.Message);
            }
            catch (TaskCanceledException)
            {
                return (false, false, TimeoutMessage);
            }
            catch (HttpRequestException)
            {
                return (false, false, UnreachableMessage);
            }
        }

        /// <summary>Jelszó-visszaállító levelet kér. A szerver mindig ugyanazt az üzenetet adja (nem árulja el, létezik-e a cím).</summary>
        public static async Task<string> ForgotPasswordAsync(string email)
        {
            var result = await PostAsync<MessageResult>("/api/forgot-password", new Dictionary<string, object?> { ["email"] = email });
            return result.Message.Length > 0
                ? result.Message
                : "Ha ez az e-mail cím regisztrálva van, elküldtük a jelszó-visszaállító linket.";
        }

        /// <summary>A saját fiók végleges törlése (anonimizálás) a jelenlegi jelszóval megerősítve.</summary>
        public static async Task DeleteAccountAsync(string currentPassword)
        {
            await SendAsync(HttpMethod.Delete, "/api/account", new Dictionary<string, object?> { ["currentPassword"] = currentPassword });

            // a szerver megszüntette a munkamenetet: a valós idejű kapcsolat elutasítását ne kezeljük lejáratként
            expiredRaised = true;
            SessionStore.Clear();
        }

        /// <summary>GET, a válasz nyers JSON-szövegével (adatexporthoz).</summary>
        public static Task<string> GetRawAsync(string path) => SendAsync(HttpMethod.Get, path, null);

        private sealed class MessageResult
        {
            [JsonPropertyName("message")] public string Message { get; set; } = "";
        }


        // ------------------------------------------------------------
        // Általános kérések (a bejelentkezett session sütivel)
        // ------------------------------------------------------------

        public static async Task<T> GetAsync<T>(string path) =>
            Deserialize<T>(await SendAsync(HttpMethod.Get, path, null));

        /// <summary>
        /// GET rövid életű gyorsítótárral (tantárgyak, oktatók): oldalváltáskor nem kell újra letölteni.
        /// Bármilyen módosító kérés (foglalás, értékelés...) és a kijelentkezés törli.
        /// </summary>
        public static async Task<T> GetCachedAsync<T>(string path)
        {
            long now = Environment.TickCount64;
            if (!Cache.TryGetValue(path, out var hit) || now - hit.Time > CacheLifetimeMs)
            {
                hit = (now, await SendAsync(HttpMethod.Get, path, null), null);
                Cache[path] = hit;
            }

            // a feldolgozott eredményt is megtartjuk, így az oldalak nem dolgozzák fel újra ugyanazt a JSON-t.
            // A visszaadott listát és elemeit ezért nem szabad módosítani (csak olvasásra használjuk őket).
            if (hit.Parsed is T parsed) return parsed;

            var value = Deserialize<T>(hit.Body);
            Cache[path] = (hit.Time, hit.Body, value);
            return value;
        }

        public static Task PostAsync(string path, object? payload = null) =>
            SendAsync(HttpMethod.Post, path, payload);

        /// <summary>POST, a válasz JSON-ját típusként visszaadja.</summary>
        public static async Task<T> PostAsync<T>(string path, object? payload = null) =>
            Deserialize<T>(await SendAsync(HttpMethod.Post, path, payload));

        /// <summary>
        /// A saját fiók adatainak mentése (név, e-mail, jelszó, tantárgyak...). Siker után a bejelentkezett
        /// felhasználó adatai is frissülnek, és a CurrentUserChanged esemény lefut.
        /// </summary>
        public static async Task SaveAccountAsync(object payload)
        {
            var result = Deserialize<AccountUpdateDto>(await SendAsync(HttpMethod.Put, "/api/account", payload));
            if (result.User is { } user)
                SetUser(new UserInfo(user.Id, user.Name, user.Email, user.Role, CurrentUser?.EmailVerified ?? true));

            // az e-mail cím módosítása után a szerver újra megerősítést kér: lekérjük a friss állapotot
            try
            {
                await RefreshUserAsync();
            }
            catch (ApiException)
            {
                // nem kritikus: a következő kérésnél kiderül
            }
        }

        /// <summary>A bejelentkezett felhasználó friss adatai (pl. megerősítette-e már az e-mail címét).</summary>
        public static async Task RefreshUserAsync()
        {
            var me = await GetAsync<MeResponse>("/api/me");
            if (me.LoggedIn && me.User != null)
                SetUser(new UserInfo(me.User.Id, me.User.Name, me.User.Email, me.User.Role, me.User.EmailVerified));
        }

        /// <summary>Új megerősítő levelet kér. Igaz, ha az e-mail cím már meg volt erősítve.</summary>
        public static async Task<bool> ResendVerificationAsync()
        {
            var result = await PostAsync<ResendResult>("/api/resend-verification");
            if (result.AlreadyVerified) await RefreshUserAsync();
            return result.AlreadyVerified;
        }

        private static void SetUser(UserInfo user)
        {
            if (CurrentUser == user) return;
            CurrentUser = user;
            CurrentUserChanged?.Invoke();
        }

        private sealed class ResendResult
        {
            [JsonPropertyName("alreadyVerified")] public bool AlreadyVerified { get; set; }
        }

        /// <summary>PATCH kérés (üzenet szerkesztése).</summary>
        public static Task PatchAsync(string path, object payload) =>
            SendAsync(HttpMethod.Patch, path, payload);

        /// <summary>Fájl feltöltése űrlapként ("file" mező), opcionális szöveggel; a válasz JSON-ját visszaadja.</summary>
        public static async Task<T> UploadAsync<T>(string path, string filePath, string? caption)
        {
            await using var stream = File.OpenRead(filePath);
            using var form = new MultipartFormDataContent();
            form.Add(new StreamContent(stream), "file", Path.GetFileName(filePath));
            if (!string.IsNullOrWhiteSpace(caption))
                form.Add(new StringContent(caption), "content");

            return Deserialize<T>(await SendAsync(HttpMethod.Post, path, null, form, FileHttp));
        }

        /// <summary>Fájl letöltése a megadott helyre (üzenet csatolmánya).</summary>
        public static async Task DownloadAsync(string path, string destinationFile)
        {
            try
            {
                using var response = await FileHttp.GetAsync(BaseUrl + path, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                    throw new ApiException(ReadError(await response.Content.ReadAsStringAsync(), response.StatusCode));

                await using var source = await response.Content.ReadAsStreamAsync();
                await using var target = File.Create(destinationFile);
                await source.CopyToAsync(target);
            }
            catch (TaskCanceledException)
            {
                throw new ApiException(TimeoutMessage);
            }
            catch (HttpRequestException)
            {
                throw new ApiException(UnreachableMessage);
            }
            catch (IOException ex)
            {
                throw new ApiException("A fájl mentése nem sikerült: " + ex.Message);
            }
        }

        public static Task DeleteAsync(string path) =>
            SendAsync(HttpMethod.Delete, path, null);

        private static async Task<string> SendAsync(HttpMethod method, string path, object? payload, HttpContent? content = null, HttpClient? client = null)
        {
            using var request = new HttpRequestMessage(method, BaseUrl + path);
            if (content != null)
                request.Content = content;
            else if (payload != null)
                request.Content = JsonContent.Create(payload);

            try
            {
                using var response = await (client ?? Http).SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    NotifySessionExpired(); // az app visszalép a belépéshez
                    throw new ApiException("A munkamenet lejárt. Jelentkezz be újra.");
                }
                if (!response.IsSuccessStatusCode)
                {
                    if (ReadCode(body) == "EMAIL_NOT_VERIFIED")
                    {
                        // a felület figyelmeztető sávja azonnal megjelenik, a hibaüzenet pedig megmondja, mit tegyen
                        if (CurrentUser is { EmailVerified: true } user)
                            SetUser(user with { EmailVerified = false });
                        throw new ApiException("Előbb erősítsd meg az e-mail címed: a megerősítő levelet az ablak tetején lévő sávban újra elküldheted.");
                    }
                    throw new ApiException(ReadError(body, response.StatusCode));
                }

                if (method != HttpMethod.Get)
                    Cache.Clear(); // az adatok változhattak (pl. új értékelés az oktatónál)

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

        internal static T Deserialize<T>(string body)
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

        /// <summary>A szerver hibakódja (pl. EMAIL_NOT_VERIFIED), ha van.</summary>
        private static string? ReadCode(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("code", out var code))
                    return code.GetString();
            }
            catch (JsonException)
            {
                // nem JSON a válasz
            }
            return null;
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

            /// <summary>Régebbi szervernél a mező hiányzik: ilyenkor megerősítettnek vesszük.</summary>
            [JsonPropertyName("emailVerified")] public bool EmailVerified { get; set; } = true;
        }
    }
}

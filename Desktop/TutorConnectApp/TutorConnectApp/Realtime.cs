using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace TutorConnectApp
{
    /// <summary>Esemény a szervertől: új / visszavont / szerkesztett üzenet, gépelés, foglalás-változás.</summary>
    internal sealed record RealtimeEvent(string Type, int ConversationId, int MessageId, MessageDto? Message);

    /// <summary>
    /// Valós idejű kapcsolat a szerverrel (WebSocket, ugyanazzal a munkamenettel, mint a REST hívások).
    /// Megszakadás esetén magától újracsatlakozik. Az események a felületi szálon érkeznek.
    /// </summary>
    internal static class Realtime
    {
        public static event Action<RealtimeEvent>? EventReceived;
        public static event Action<bool>? ConnectionChanged;

        private static CancellationTokenSource? cts;
        private static ClientWebSocket? socket;
        private static SynchronizationContext? ui;

        public static bool IsConnected { get; private set; }

        /// <summary>Elindítja a kapcsolatot (belépés után, a felületi szálról).</summary>
        public static void Start()
        {
            Stop();
            ui = SynchronizationContext.Current;
            cts = new CancellationTokenSource();
            _ = RunAsync(cts.Token);
        }

        public static void Stop()
        {
            cts?.Cancel();
            cts = null;

            var old = socket;
            socket = null;
            try { old?.Dispose(); } catch (ObjectDisposedException) { }

            SetConnected(false);
        }

        /// <summary>Jelzi a másik félnek, hogy gépelsz (a hívó korlátozza a gyakoriságot).</summary>
        public static void SendTyping(int conversationId)
        {
            var ws = socket;
            if (ws is not { State: WebSocketState.Open }) return;

            byte[] data = Encoding.UTF8.GetBytes($"{{\"type\":\"typing\",\"conversation_id\":{conversationId}}}");
            _ = SendAsync(ws, data);
        }

        private static async Task SendAsync(ClientWebSocket ws, byte[] data)
        {
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or InvalidOperationException)
            {
                // a kapcsolat közben megszakadt; az újracsatlakozás elintézi
            }
        }

        private static async Task RunAsync(CancellationToken token)
        {
            int delay = 1000;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var ws = new ClientWebSocket();
                    ws.Options.Cookies = ApiClient.Cookies;
                    await ws.ConnectAsync(ToSocketUri(), token);

                    socket = ws;
                    delay = 1000;
                    Post(() => SetConnected(true));

                    while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                    {
                        string? text = await ReceiveTextAsync(ws, token);
                        if (text == null) break;
                        Handle(text);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex) when (ex is WebSocketException or IOException or HttpRequestException or ObjectDisposedException)
                {
                    // 401: a szerver elutasította a munkamenetet (lejárt): a felület visszalép a belépéshez
                    if (ex.Message.Contains("401"))
                        Post(ApiClient.NotifySessionExpired);

                    // egyébként nem elérhető a szerver, vagy megszakadt a kapcsolat: újrapróbáljuk
                }

                socket = null;
                Post(() => SetConnected(false));

                try { await Task.Delay(delay, token); }
                catch (OperationCanceledException) { return; }
                delay = Math.Min(delay * 2, 15000);
            }
        }

        private static Uri ToSocketUri() => new("ws" + ApiClient.BaseUrl[4..]); // http → ws, https → wss

        private static async Task<string?> ReceiveTextAsync(ClientWebSocket ws, CancellationToken token)
        {
            var buffer = new byte[8192];
            using var message = new MemoryStream();

            while (true)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close) return null;

                message.Write(buffer, 0, result.Count);
                if (result.EndOfMessage) break;
            }
            return Encoding.UTF8.GetString(message.ToArray());
        }

        private static void Handle(string json)
        {
            RealtimeEvent? evt = null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var typeProp)) return;

                string type = typeProp.GetString() ?? "";
                int conversationId = root.TryGetProperty("conversation_id", out var c) && c.TryGetInt32(out int cid) ? cid : 0;
                int messageId = root.TryGetProperty("message_id", out var m) && m.TryGetInt32(out int mid) ? mid : 0;

                MessageDto? message = null;
                if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object)
                {
                    message = ApiClient.Deserialize<MessageDto>(msg.GetRawText());
                    conversationId = message.ConversationId;
                    messageId = message.Id;
                }

                evt = new RealtimeEvent(type, conversationId, messageId, message);
            }
            catch (Exception ex) when (ex is JsonException or ApiException)
            {
                return; // ismeretlen vagy hibás üzenet: figyelmen kívül hagyjuk
            }

            Post(() => EventReceived?.Invoke(evt!));
        }

        private static void SetConnected(bool connected)
        {
            if (IsConnected == connected) return;
            IsConnected = connected;
            ConnectionChanged?.Invoke(connected);
        }

        private static void Post(Action action)
        {
            if (ui != null) ui.Post(_ => action(), null);
            else action();
        }
    }
}

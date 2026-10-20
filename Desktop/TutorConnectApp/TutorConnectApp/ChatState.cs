using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>Az alkalmazás szintű "frissítsd a jelvényeket" jelzés (olvasatlan üzenetek, függő foglalások).</summary>
    internal static class AppEvents
    {
        public static event Action? BadgesStale;

        public static void RaiseBadgesStale() => BadgesStale?.Invoke();
    }

    /// <summary>
    /// A beszélgetések közös, memóriabeli állapota: ebből számolódik a navbár olvasatlan-jelvénye is,
    /// ezért a jelvényhez nem kell minden eseménynél újra letölteni a teljes listát.
    /// A valós idejű események a főablakon keresztül frissítik; a Chat oldal a Changed eseményre rajzol újra.
    /// </summary>
    internal static class ChatState
    {
        private static List<ConversationDto> conversations = new();
        private static int requestCounter;
        private static int appliedRequest;

        public static IReadOnlyList<ConversationDto> Conversations => conversations;

        public static int TotalUnread => conversations.Sum(c => c.UnreadCount);

        /// <summary>Igaz, ha a lista már legalább egyszer betöltődött.</summary>
        public static bool Loaded { get; private set; }

        /// <summary>Az utolsó sikeres betöltés ideje (Environment.TickCount64).</summary>
        public static long LastLoadedTick { get; private set; }

        /// <summary>A Chat oldalon jelenleg látható (megnyitott) beszélgetés – annak üzenetei nem számítanak olvasatlannak.</summary>
        public static int? OpenConversationId { get; set; }

        /// <summary>Az a Chat oldal, amelyik az OpenConversationId-t beállította (oldalcsere közben két oldal élhet egyszerre).</summary>
        public static object? OpenOwner { get; set; }

        public static event Action? Changed;

        public static ConversationDto? Find(int id) => conversations.FirstOrDefault(c => c.Id == id);

        public static async Task LoadAsync()
        {
            int request = ++requestCounter;
            var list = await ApiClient.GetAsync<List<ConversationDto>>("/api/conversations");

            // ha közben egy újabb lekérés már megérkezett, ezt a régebbi választ eldobjuk
            if (request < appliedRequest) return;
            appliedRequest = request;

            conversations = Sort(list);
            Loaded = true;
            LastLoadedTick = Environment.TickCount64;
            Changed?.Invoke();
        }

        /// <summary>Kijelentkezéskor: a következő felhasználónak ne maradjon meg semmi.</summary>
        public static void Clear()
        {
            requestCounter++;
            appliedRequest = requestCounter;
            conversations = new List<ConversationDto>();
            Loaded = false;
            OpenConversationId = null;
            OpenOwner = null;
            Changed?.Invoke();
        }

        private static List<ConversationDto> Sort(IEnumerable<ConversationDto> list) => list
            .OrderByDescending(c => c.LastMessageTime ?? DateTime.MinValue)
            .ThenByDescending(c => c.Id)
            .ToList();

        /// <summary>A beszélgetés előnézetét és idejét frissíti, és előre teszi a listában.</summary>
        public static void Bump(ConversationDto conversation, string preview, DateTime time)
        {
            conversation.LastMessage = preview;
            conversation.LastMessageTime = time;
            conversation.LastMessageRetractedValue = 0;
            conversations = Sort(conversations);
            Changed?.Invoke();
        }

        /// <summary>Csak az előnézet szövegét módosítja (szerkesztés / visszavonás után), a sorrend nem változik.</summary>
        public static void UpdatePreview(ConversationDto conversation, string preview, bool retracted)
        {
            conversation.LastMessage = preview;
            conversation.LastMessageRetractedValue = retracted ? 1 : 0;
            Changed?.Invoke();
        }

        public static void MarkRead(ConversationDto conversation)
        {
            if (conversation.UnreadCount == 0) return;
            conversation.UnreadCount = 0;
            Changed?.Invoke();
        }

        /// <summary>Új beérkező üzenet: olvasatlan számláló és előnézet. Ismeretlen beszélgetésnél letölti a listát.</summary>
        public static async Task OnNewMessageAsync(MessageDto message, int myId)
        {
            var conversation = Find(message.ConversationId);
            if (conversation == null)
            {
                await LoadAsync(); // új beszélgetés (az oktatónál az első üzenettel jelenik meg)
                return;
            }

            if (message.SenderId != myId && OpenConversationId != conversation.Id)
                conversation.UnreadCount++;
            Bump(conversation, message.Content, message.CreatedAt);
        }

        /// <summary>
        /// Szerkesztés / visszavonás esemény. A megnyitott beszélgetést a Chat oldal helyben frissíti,
        /// minden másnál a lista újratöltése a legegyszerűbb (ritka esemény).
        /// </summary>
        public static async Task OnMessageChangedAsync(int conversationId)
        {
            if (OpenConversationId == conversationId) return;
            await LoadAsync();
        }
    }
}

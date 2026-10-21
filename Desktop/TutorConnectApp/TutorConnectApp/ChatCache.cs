namespace TutorConnectApp
{
    /// <summary>
    /// A már letöltött üzenetek memóriabeli gyorsítótára beszélgetésenként. A beszélgetés megnyitásakor a korábban
    /// látott üzenetek azonnal megjelennek, a friss állapot a háttérben érkezik; a keresés is ebből dolgozik.
    /// A valós idejű események a főablakon keresztül frissítik (a Chat oldaltól függetlenül).
    /// </summary>
    internal static class ChatMessages
    {
        /// <summary>Ennyi beszélgetés üzeneteit tartjuk a memóriában (a legrégebben használtak kiesnek).</summary>
        private const int MaxConversations = 40;

        private static readonly Dictionary<int, List<MessageDto>> Cache = new();
        private static readonly List<int> Usage = new(); // a legrégebben használt elöl
        private static readonly Dictionary<int, Task<List<MessageDto>>> Loading = new();

        public static bool TryGet(int conversationId, out List<MessageDto> messages)
        {
            if (Cache.TryGetValue(conversationId, out var list))
            {
                Touch(conversationId);
                messages = list;
                return true;
            }

            messages = new List<MessageDto>();
            return false;
        }

        /// <summary>Eltárolja a beszélgetés üzeneteit (a még úton lévő, szerver által nem ismert üzenetek nélkül).</summary>
        public static void Set(int conversationId, IEnumerable<MessageDto> messages)
        {
            Cache[conversationId] = messages.Where(m => m.Id > 0).ToList();
            Touch(conversationId);

            while (Usage.Count > MaxConversations)
            {
                int oldest = Usage[0];
                Usage.RemoveAt(0);
                Cache.Remove(oldest);
            }
        }

        /// <summary>Új üzenet a gyorsítótárba (csak ha a beszélgetés már benne van, és az üzenet még nincs).</summary>
        public static void Add(MessageDto message)
        {
            if (message.Id <= 0 || !Cache.TryGetValue(message.ConversationId, out var list)) return;
            if (list.Any(m => m.Id == message.Id)) return;
            list.Add(message);
        }

        public static void Remove(int conversationId)
        {
            Cache.Remove(conversationId);
            Usage.Remove(conversationId);
        }

        public static void Clear()
        {
            Cache.Clear();
            Usage.Clear();
            Loading.Clear();
        }

        /// <summary>A beszélgetés üzenetei: a gyorsítótárból, vagy letöltve (a párhuzamos kérések egyetlen letöltésen osztoznak).</summary>
        public static async Task<List<MessageDto>> GetOrLoadAsync(int conversationId)
        {
            if (TryGet(conversationId, out var cached)) return cached;

            if (!Loading.TryGetValue(conversationId, out var task))
            {
                task = LoadAsync(conversationId);
                Loading[conversationId] = task;
            }
            return await task;
        }

        private static async Task<List<MessageDto>> LoadAsync(int conversationId)
        {
            try
            {
                var list = await ApiClient.GetAsync<List<MessageDto>>($"/api/conversations/{conversationId}/messages");

                // a letöltés alatt megnyitott beszélgetés már magát is eltárolhatta: azt nem írjuk felül
                if (!Cache.ContainsKey(conversationId)) Set(conversationId, list);
                return Cache.TryGetValue(conversationId, out var stored) ? stored : list;
            }
            finally
            {
                Loading.Remove(conversationId);
            }
        }

        private static void Touch(int conversationId)
        {
            Usage.Remove(conversationId);
            Usage.Add(conversationId);
        }
    }
}

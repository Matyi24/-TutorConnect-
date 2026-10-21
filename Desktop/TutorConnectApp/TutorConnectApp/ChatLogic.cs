using System.Globalization;
using System.Text.RegularExpressions;

namespace TutorConnectApp
{
    /// <summary>
    /// Kis- és nagybetűre, ékezetre érzéketlen szövegkeresés. Az ékezetek a nyelvtől függetlenül elhanyagolódnak
    /// ("o" megtalálja az "ö"-t, "ő"-t és "ó"-t is), így a gyorskeresés a magyar billentyűzet nélkül is használható.
    /// </summary>
    internal static class TextSearch
    {
        private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth;

        private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

        public static bool Contains(string text, string query) =>
            query.Length == 0 || Compare.IndexOf(text, query, Options) >= 0;

        /// <summary>Az első találat helye és hossza a szövegben (a hossz a szöveg karaktereiben értendő); null, ha nincs.</summary>
        public static (int Index, int Length)? Find(string text, string query)
        {
            if (query.Length == 0 || text.Length == 0) return null;

            int index = Compare.IndexOf(text.AsSpan(), query.AsSpan(), Options, out int length);
            return index < 0 ? null : (index, Math.Max(length, 1));
        }

        /// <summary>
        /// Rövid részlet a találat körül (egy sorban, a sortörések szóközzé alakítva), a kihagyott részek helyén "…".
        /// A visszaadott helyzet a részleten belül mutatja a találatot.
        /// </summary>
        public static (string Text, int MatchStart, int MatchLength) Snippet(string text, int matchIndex, int matchLength, int radius = 28)
        {
            // karakterenként cserélünk (a hossz nem változik), így a találat helye a szövegben ugyanaz marad
            string flat = text.Replace('\r', ' ').Replace('\n', ' ');

            int start = Math.Max(0, matchIndex - radius);
            int end = Math.Min(flat.Length, matchIndex + matchLength + radius * 2);
            string piece = flat[start..end];

            string prefix = start > 0 ? "…" : "";
            string suffix = end < flat.Length ? "…" : "";
            return (prefix + piece + suffix, prefix.Length + (matchIndex - start), matchLength);
        }
    }

    /// <summary>Egy találat az üzenetek közötti keresésben.</summary>
    internal sealed record MessageHit(ConversationDto Conversation, MessageDto Message, string Snippet, int MatchStart, int MatchLength);

    /// <summary>A chat kijelzési és keresési szabályai (felülettől független, ezért tesztelhető).</summary>
    internal static class ChatLogic
    {
        /// <summary>Ennyi időn belül egymás után küldött, azonos feladójú üzenetek vizuálisan egy csoportot alkotnak.</summary>
        public static readonly TimeSpan GroupGap = TimeSpan.FromMinutes(5);

        /// <summary>Az üzenet az előzővel egy csoportba tartozik-e (azonos feladó, azonos nap, rövid időn belül).</summary>
        public static bool IsGrouped(MessageDto? previous, MessageDto current)
        {
            if (previous == null || previous.SenderId != current.SenderId) return false;
            if (previous.CreatedAt.ToLocalTime().Date != current.CreatedAt.ToLocalTime().Date) return false;

            var gap = current.CreatedAt - previous.CreatedAt;
            return gap >= TimeSpan.Zero && gap <= GroupGap;
        }

        /// <summary>Az első, még nem olvasott, másiktól érkezett üzenet helye; -1, ha nincs ilyen.</summary>
        public static int FirstUnreadIndex(IReadOnlyList<MessageDto> messages, int myId, int from = 0)
        {
            for (int i = Math.Max(from, 0); i < messages.Count; i++)
            {
                var m = messages[i];
                if (m.SenderId != myId && !m.IsRead && !m.IsRetracted && m.Id > 0)
                    return i;
            }
            return -1;
        }

        public static string DayLabel(DateTime day, DateTime today)
        {
            if (day == today) return "Ma";
            if (day == today.AddDays(-1)) return "Tegnap";
            return day.ToString("yyyy. MMMM d., dddd", CultureInfo.GetCultureInfo("hu-HU"));
        }

        private static readonly Regex LinkPattern = new(@"(?:https?://|www\.)[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Az üzenetben szereplő http(s) hivatkozások (legfeljebb három, egyedi, megnyitásra kész alakban).</summary>
        public static List<string> ExtractLinks(string? text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            foreach (Match match in LinkPattern.Matches(text))
            {
                string raw = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}', '"', '\'');
                if (raw.Length == 0) continue;
                if (raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) raw = "https://" + raw;

                // csak valódi, http(s) címet nyitunk meg (egyéb protokoll nem futtathat semmit)
                if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) continue;
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) continue;

                string link = uri.AbsoluteUri;
                if (!result.Contains(link)) result.Add(link);
                if (result.Count == 3) break;
            }
            return result;
        }

        /// <summary>Keresés a már ismert üzenetekben: a legújabb találatok elöl, a visszavont üzenetek kimaradnak.</summary>
        public static List<MessageHit> Search(IEnumerable<(ConversationDto Conversation, IReadOnlyList<MessageDto> Messages)> source, string query, int max = 60)
        {
            var hits = new List<MessageHit>();
            query = query.Trim();
            if (query.Length == 0) return hits;

            foreach (var (conversation, messages) in source)
            {
                foreach (var message in messages)
                {
                    if (message.IsRetracted || message.Content.Length == 0) continue;
                    if (TextSearch.Find(message.Content, query) is not { } found) continue;

                    var (text, start, length) = TextSearch.Snippet(message.Content, found.Index, found.Length);
                    hits.Add(new MessageHit(conversation, message, text, start, length));
                }
            }

            return hits
                .OrderByDescending(h => h.Message.CreatedAt)
                .ThenByDescending(h => h.Message.Id)
                .Take(max)
                .ToList();
        }

        /// <summary>
        /// Egyesíti a szerverről frissen letöltött üzeneteket a helyben lévőkkel: az időközben (a letöltés alatt) érkezett
        /// üzenetek és a még úton lévő (küldés alatt álló, vagy hibás) üzenetek nem vesznek el.
        /// </summary>
        public static List<MessageDto> Merge(IReadOnlyList<MessageDto> fresh, IReadOnlyList<MessageDto> local)
        {
            var result = new List<MessageDto>(fresh);
            int maxFresh = fresh.Count == 0 ? 0 : fresh.Max(m => m.Id);
            var known = fresh.Select(m => m.Id).ToHashSet();

            foreach (var message in local)
            {
                bool pending = message.Id < 0;
                bool newer = message.Id > maxFresh && !known.Contains(message.Id);
                if (pending || newer) result.Add(message);
            }
            return result;
        }
    }
}

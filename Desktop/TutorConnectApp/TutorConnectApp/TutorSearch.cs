namespace TutorConnectApp
{
    /// <summary>
    /// Javaslatok az oktató-keresőhöz ("erre gondoltál?"): a begépelt szövegre a legjobb egyezések, ékezetek és kis-nagybetű nélkül.
    /// Sorrend: a név eleje, a név valamelyik szavának eleje, a név belseje, végül a tantárgy; azon belül a jobb értékelés előrébb.
    /// </summary>
    internal static class TutorSearch
    {
        public static List<TutorDto> Suggest(IEnumerable<TutorDto> tutors, string query, int max = 6)
        {
            query = query.Trim();
            if (query.Length == 0) return new List<TutorDto>();

            return tutors
                .Select(t => (Tutor: t, Score: Score(t, query)))
                .Where(x => x.Score >= 0)
                .OrderBy(x => x.Score)
                .ThenByDescending(x => x.Tutor.ReviewCount > 0 ? x.Tutor.AverageRating : 0m)
                .ThenBy(x => x.Tutor.FullName, StringComparer.CurrentCultureIgnoreCase)
                .Take(max)
                .Select(x => x.Tutor)
                .ToList();
        }

        /// <summary>Hány oktató egyezik összesen (a "további találat" jelzéshez).</summary>
        public static int CountMatches(IEnumerable<TutorDto> tutors, string query) =>
            query.Trim().Length == 0 ? 0 : tutors.Count(t => Score(t, query.Trim()) >= 0);

        private static int Score(TutorDto tutor, string query)
        {
            string name = tutor.FullName ?? "";
            if (TextSearch.Find(name, query) is { } found)
            {
                if (found.Index == 0) return 0;

                // a név valamelyik szavának eleje (pl. "Anna" a "Nagy Anna" névben)
                if (name[found.Index - 1] is ' ' or '-' or '.') return 1;
                return 2;
            }

            return TextSearch.Contains(tutor.Subjects ?? "", query) ? 3 : -1;
        }
    }
}

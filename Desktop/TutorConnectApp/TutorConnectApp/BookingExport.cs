using System.Drawing.Printing;
using System.Text;

namespace TutorConnectApp
{
    /// <summary>Foglalási előzmények mentése CSV-be és az órarend kinyomtatása (nyomtatási kép előnézettel).</summary>
    internal static class BookingExport
    {
        /// <summary>Az összes foglalás (függő, közelgő, lezajlott, lemondott) letöltése és mentése CSV fájlba.</summary>
        public static async Task ExportAllAsync(IWin32Window? owner, string role)
        {
            try
            {
                var all = await FetchAllAsync();
                if (all.Count == 0)
                {
                    MessageBox.Show(owner, "Még nincs foglalásod, amit exportálni lehetne.", "Exportálás", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dialog = new SaveFileDialog
                {
                    Title = "Foglalási előzmények exportálása",
                    Filter = "CSV fájl (*.csv)|*.csv",
                    FileName = $"tutorconnect-foglalasok-{DateTime.Today:yyyy-MM-dd}.csv"
                };
                if (dialog.ShowDialog(owner) != DialogResult.OK) return;

                // BOM-mal, hogy az Excel az ékezeteket helyesen olvassa
                File.WriteAllText(dialog.FileName, CsvExport.Build(all, role), new UTF8Encoding(true));
                MessageBox.Show(owner, $"{all.Count} foglalás elmentve. A fájl Excelben megnyitható.", "Exportálás kész",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(owner, ex.Message, "Nem sikerült az exportálás", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(owner, "A fájl nem menthető: " + ex.Message, "Nem sikerült az exportálás", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>A függő és közelgő órák órarendje nyomtatási képként (innen nyomtatható, vagy PDF-be menthető).</summary>
        public static async Task PrintTimetableAsync(IWin32Window? owner, string userName)
        {
            try
            {
                var pendingTask = ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=pending");
                var upcomingTask = ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=upcoming");
                await Task.WhenAll(pendingTask, upcomingTask);

                var days = TimetableLogic.Build(pendingTask.Result.Concat(upcomingTask.Result), DateTime.Today);
                if (days.Count == 0)
                {
                    MessageBox.Show(owner, "Most nincs közelgő órád, így nincs mit kinyomtatni.", "Órarend", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                TimetablePrinter.ShowPreview(owner, userName, days);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(owner, ex.Message, "Nem sikerült az órarend elkészítése", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static async Task<List<BookingDto>> FetchAllAsync()
        {
            var tasks = new[] { "pending", "upcoming", "past", "cancelled" }
                .Select(status => ApiClient.GetAsync<List<BookingDto>>($"/api/bookings?status={status}"))
                .ToArray();
            await Task.WhenAll(tasks);
            return tasks.SelectMany(t => t.Result).GroupBy(b => b.Id).Select(g => g.First()).ToList();
        }
    }

    /// <summary>Az órarend nyomtatása: napok szerint csoportosított lista, oldaltöréssel és oldalszámmal.</summary>
    internal static class TimetablePrinter
    {
        private abstract record Line;
        private sealed record DayLine(DateTime Date) : Line;
        private sealed record EntryLine(TimetableEntry Entry) : Line;

        public static void ShowPreview(IWin32Window? owner, string userName, List<TimetableDay> days)
        {
            var lines = new List<Line>();
            foreach (var day in days)
            {
                lines.Add(new DayLine(day.Date));
                lines.AddRange(day.Entries.Select(e => new EntryLine(e)));
            }

            using var document = new PrintDocument { DocumentName = "TutorConnect órarend" };
            document.DefaultPageSettings.Margins = new Margins(70, 70, 70, 70); // század hüvelyk
            int next = 0;
            int page = 0;

            // minden nyomtatás (az előnézet és a Nyomtatás gomb is) az elejéről indul
            document.BeginPrint += (_, _) =>
            {
                next = 0;
                page = 0;
            };

            document.PrintPage += (_, e) =>
            {
                var g = e.Graphics!;
                var area = e.MarginBounds;
                page++;

                using var titleFont = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point);
                using var subFont = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
                using var dayFont = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
                using var textFont = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
                using var boldFont = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
                using var mutedFont = new Font("Segoe UI", 9.5F, FontStyle.Italic, GraphicsUnit.Point);
                using var ink = new SolidBrush(Color.Black);
                using var gray = new SolidBrush(Color.FromArgb(90, 90, 90));
                using var accent = new SolidBrush(Color.FromArgb(59, 91, 219));
                using var rule = new Pen(Color.FromArgb(180, 180, 180));

                float y = area.Top;
                if (page == 1)
                {
                    g.DrawString("Órarend", titleFont, accent, area.Left, y);
                    y += titleFont.GetHeight(g) + 2;
                    g.DrawString($"{userName}  ·  készült: {DateTime.Now.ToString("yyyy. MM. dd. HH:mm", Ui.Hu)}", subFont, gray, area.Left, y);
                    y += subFont.GetHeight(g) + 14;
                    g.DrawLine(rule, area.Left, y, area.Right, y);
                    y += 16;
                }

                float footer = subFont.GetHeight(g) + 8;
                float limit = area.Bottom - footer;

                while (next < lines.Count)
                {
                    var line = lines[next];
                    float height = line is DayLine ? dayFont.GetHeight(g) + 14 : textFont.GetHeight(g) + 6;

                    // a napfejléc ne maradjon egyedül az oldal alján
                    float needed = line is DayLine ? height + textFont.GetHeight(g) + 6 : height;
                    if (y + needed > limit) break;

                    if (line is DayLine dayLine)
                    {
                        y += 10;
                        g.DrawString(dayLine.Date.ToString("yyyy. MMMM d., dddd", Ui.Hu), dayFont, ink, area.Left, y);
                        y += dayFont.GetHeight(g) + 4;
                        g.DrawLine(rule, area.Left, y, area.Right, y);
                        y += 4;
                    }
                    else if (line is EntryLine entryLine)
                    {
                        var entry = entryLine.Entry;
                        g.DrawString(entry.Time, boldFont, ink, area.Left + 12, y);
                        g.DrawString(entry.Subject, textFont, ink, area.Left + 140, y);
                        g.DrawString(entry.Partner, textFont, ink, area.Left + 360, y);
                        if (!entry.Confirmed)
                            g.DrawString(entry.Status, mutedFont, gray, area.Right - 110, y + 1);
                        y += height;
                    }
                    next++;
                }

                g.DrawString($"{page}. oldal", subFont, gray, area.Right - 60, area.Bottom - subFont.GetHeight(g));
                e.HasMorePages = next < lines.Count;
            };

            using var preview = new PrintPreviewDialog
            {
                Document = document,
                UseAntiAlias = true,
                Width = 1000,
                Height = 800,
                StartPosition = FormStartPosition.CenterParent,
                Text = "Órarend – nyomtatási kép"
            };
            try
            {
                preview.ShowDialog(owner);
            }
            catch (InvalidPrinterException)
            {
                MessageBox.Show(owner, "Nincs elérhető nyomtató. Telepíts egyet, vagy válaszd a \"Microsoft Print to PDF\" nyomtatót.",
                    "Órarend", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}

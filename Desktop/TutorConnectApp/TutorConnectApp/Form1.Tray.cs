using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace TutorConnectApp
{
    // Tálcaikon, értesítések, emlékeztetők és kedvenc-figyelés
    public partial class TutorMain
    {
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        /// <summary>A tálcaikon piros jelvényt kap az olvasatlan üzenetek számával, a súgószövege is mutatja.</summary>
        private void UpdateTrayIcon(int unread)
        {
            var previous = badgedIcon;
            badgedIcon = null;

            if (unread > 0)
            {
                using var bitmap = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.DrawIcon(trayIcon ?? SystemIcons.Application, new Rectangle(0, 0, 32, 32));

                    var dot = new Rectangle(12, 12, 20, 20);
                    using (var red = new SolidBrush(Theme.Danger))
                        g.FillEllipse(red, dot);
                    using var font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var white = new SolidBrush(Color.White);
                    using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(unread > 9 ? "9+" : unread.ToString(), font, white, dot, center);
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using var temp = Icon.FromHandle(handle);
                    badgedIcon = (Icon)temp.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }

            tray.Icon = badgedIcon ?? trayIcon ?? SystemIcons.Application;
            tray.Text = unread > 0 ? Shorten($"TutorConnect – {unread} olvasatlan üzenet", 60) : "TutorConnect";
            previous?.Dispose();
        }

        private void BuildTray()
        {
            try
            {
                trayIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException)
            {
                trayIcon = null; // a rendszer alapértelmezett ikonja marad
            }

            tray.Icon = trayIcon ?? SystemIcons.Application;
            tray.Text = "TutorConnect";

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Megnyitás", null, (_, _) => RestoreFromTray("", null));
            trayMenu.Items.Add("Kilépés", null, (_, _) =>
            {
                quitting = true;
                Close();
            });
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += (_, _) => RestoreFromTray("", null);
            tray.BalloonTipClicked += (_, _) => RestoreFromTray(trayTarget, trayConversationId);
            tray.Visible = true;
        }

        /// <summary>Előhozza az ablakot, és (ha kell) a kért oldalra, illetve beszélgetésre ugrik.</summary>
        private void RestoreFromTray(string target, int? targetId)
        {
            if (IsDisposed) return;

            if (!Visible) Show(); // a tálcára rejtett ablak visszahozása
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();

            if (target.Length == 0) return;
            if (target == "chat" && targetId is int conversationId)
                ChatPage.PendingConversationId = conversationId;
            if (target == "calendar" && targetId is int tutorId)
                CalendarPage.InitialTutorId = tutorId;
            if (target == "reviews")
            {
                BookingsPage.RequestedTab = "past";
                target = "bookings";
            }
            navBar.Select(target);
        }

        /// <summary>Igaz, ha az alkalmazás egyik ablaka sem aktív (háttérben van vagy le van kicsinyítve).</summary>
        private bool InBackground => WindowState == FormWindowState.Minimized || ActiveForm == null;

        /// <param name="always">Igaz: akkor is jelez, ha az ablak előtérben van (pl. kedvenc oktató új időpontja).</param>
        private void ShowToast(string title, string text, string target, int? conversationId = null, bool always = false)
        {
            if (IsDisposed || !AppSettings.NotificationsEnabled || (!always && !InBackground)) return;

            trayTarget = target;
            trayConversationId = conversationId;
            tray.ShowBalloonTip(6000, Shorten(title, 60), Shorten(text, 200), ToolTipIcon.Info);
        }

        private static string Shorten(string text, int max)
        {
            text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= max ? text : text[..(max - 1)] + "…";
        }

        private void NotifyNewMessage(MessageDto message)
        {
            if (message.SenderId == Me.Id) return;

            string name = ChatState.Find(message.ConversationId)?.OtherUserName ?? "";
            bool onlyFile = message.HasAttachment && (string.IsNullOrWhiteSpace(message.Content) || message.Content == message.AttachmentName);
            string body = onlyFile ? "Csatolmány: " + message.AttachmentName : message.Content;

            ShowToast(name.Length > 0 ? $"Új üzenet: {name}" : "Új üzenet", body, "chat", message.ConversationId);
        }

        /// <summary>
        /// A kedvenc oktatók szabad időpontjainak lekérése. Ha olyan időpont jelent meg, amit az előző ellenőrzéskor
        /// még nem láttunk, értesítést kapunk. Az első ellenőrzés csak az alapállapotot rögzíti.
        /// </summary>
        private async Task CheckFavoritesAsync()
        {
            if (favoriteChecking || IsDisposed || Me.Role != "STUDENT") return;

            var ids = Favorites.Ids.ToList();
            if (ids.Count == 0) return;

            favoriteChecking = true;
            try
            {
                foreach (int tutorId in ids)
                {
                    try
                    {
                        var data = await ApiClient.GetAsync<AvailabilityDto>($"/api/tutors/{tutorId}/availability");
                        if (IsDisposed) return;

                        var current = data.Slots.Select(s => s.Id).ToList();
                        var known = Favorites.GetKnown(tutorId);
                        Favorites.SetKnown(tutorId, current);

                        int added = known == null ? 0 : current.Count(id => !known.Contains(id));
                        if (added == 0 || !AppSettings.FavoriteAlertsEnabled) continue;

                        var tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                        string name = tutors.FirstOrDefault(t => t.Id == tutorId)?.FullName ?? "Egy kedvenc oktatód";
                        ShowToast("Új szabad időpont",
                            added == 1 ? $"{name} új szabad időpontot hirdetett." : $"{name} {added} új szabad időpontot hirdetett.",
                            "calendar", tutorId, always: true);
                    }
                    catch (ApiException)
                    {
                        // egy oktató lekérése nem sikerült (pl. törölt fiók): a többit tovább ellenőrizzük
                    }
                }
            }
            finally
            {
                favoriteChecking = false;
            }
        }

        /// <summary>
        /// Emlékeztető a közelgő órákra. A lista tíz percenként (és foglalás-változáskor) frissül, az értesítés
        /// az óra előtt 1 órával és 10 perccel jön, az ablak állapotától függetlenül.
        /// </summary>
        private async Task CheckRemindersAsync()
        {
            if (reminderChecking || IsDisposed || Me.Role is not ("STUDENT" or "TUTOR")) return;

            reminderChecking = true;
            try
            {
                if (reminders.NeedsRefresh)
                    await reminders.RefreshAsync();
                if (IsDisposed) return;

                if (AppSettings.RemindersEnabled)
                {
                    foreach (var (booking, minutes, final) in reminders.Due())
                    {
                        string who = (booking.AsStudent ? "Oktató: " : "Diák: ") + (booking.OtherName ?? "ismeretlen");
                        string title = final ? "Az órád hamarosan kezdődik" : "Az órád 1 órán belül kezdődik";
                        string text = $"{booking.SubjectName ?? "Óra"} · {who}\n{Ui.FormatRange(booking.StartTime, booking.EndTime)}  ({minutes} perc múlva)";
                        ShowToast(title, text, "bookings", always: true);
                    }
                }

                await CheckReviewPromptsAsync();
            }
            catch (ApiException)
            {
                // nincs kapcsolat: a következő ellenőrzésnél újra próbálkozik
            }
            finally
            {
                reminderChecking = false;
            }
        }

        /// <summary>
        /// A diák értesítést kap a lezajlott, még nem értékelt óráiról (tíz percenként ellenőrizzük; óránként legfeljebb egy értesítés,
        /// ugyanarról az óráról csak egyszer). A főoldalon az emlékeztető addig látszik, amíg az értékelés meg nem születik.
        /// </summary>
        private async Task CheckReviewPromptsAsync()
        {
            if (Me.Role != "STUDENT" || !AppSettings.ReviewPromptsEnabled) return;
            if (lastReviewCheck != 0 && Environment.TickCount64 - lastReviewCheck < 10 * 60_000L) return;
            lastReviewCheck = Environment.TickCount64;

            var completed = await ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=past");
            if (IsDisposed) return;

            var fresh = ReviewPrompts.NotNotified(ReviewPrompts.Pending(completed, DateTime.UtcNow));
            if (fresh.Count == 0) return;

            var first = fresh[0];
            string text = fresh.Count == 1
                ? $"{first.SubjectName ?? "Óra"} · {first.OtherName ?? "oktató"}: hogy sikerült az óra?"
                : $"{fresh.Count} lezajlott órát még nem értékeltél. Segítesz másoknak a választásban!";
            ShowToast(fresh.Count == 1 ? "Értékeld az órát" : "Értékeld az óráidat", text, "reviews", always: true);
            ReviewPrompts.MarkNotified(fresh);
        }

        private void NotifyBookingChange()
        {
            // egymás utáni események (több foglalás) ne záporozzanak értesítésként
            long now = Environment.TickCount64;
            if (now - lastBookingToast < 10_000) return;
            lastBookingToast = now;

            ShowToast("Foglalás", "Változás történt a foglalásaidban.", "bookings");
        }
    }
}

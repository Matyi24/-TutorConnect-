using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace TutorConnectApp
{
    // Billentyűparancsok és gyorskereső
    public partial class TutorMain
    {
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.K))
            {
                _ = OpenQuickSwitcherAsync();
                return true;
            }

            if (keyData == Keys.F1)
            {
                ShowShortcuts();
                return true;
            }

            if (keyData == (Keys.Control | Keys.F) && ActiveForm == this && currentPage?.FocusSearch() == true)
                return true;

            for (int i = 0; i < pageOrder.Count && i < 9; i++)
            {
                if (keyData == (Keys.Control | (Keys.D1 + i)) || keyData == (Keys.Control | (Keys.NumPad1 + i)))
                {
                    navBar.Select(pageOrder[i]);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ShowShortcuts()
        {
            if (IsDisposed || ActiveForm != this) return; // másik ablak (dialógus) fölött nem nyílik

            using var dialog = new ShortcutsDialog(Me.Role);
            dialog.ShowDialog(this);
        }

        private async Task OpenQuickSwitcherAsync()
        {
            if (IsDisposed || ActiveForm != this) return; // másik ablak (dialógus) fölött nem nyílik

            var entries = new List<QuickEntry>();
            for (int i = 0; i < pageOrder.Count; i++)
            {
                string key = pageOrder[i];
                entries.Add(new QuickEntry("page", key, 0, pageTitles[key], i < 9 ? $"Oldal · Ctrl+{i + 1}" : "Oldal", "→"));
            }
            entries.Add(new QuickEntry("action", "theme", 0, "Téma váltása", "Sötét / világos mód", "◐"));
            entries.Add(new QuickEntry("action", "shortcuts", 0, "Billentyűparancsok", "Súgó · F1", "⌨"));
            entries.Add(new QuickEntry("action", "logout", 0, "Kijelentkezés", "Vissza a belépőhöz", "↩"));

            using var dialog = new QuickSwitcherDialog(entries);
            if (Me.Role == "STUDENT") _ = AddTutorsToSwitcherAsync(dialog);

            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Chosen is not { } chosen || IsDisposed) return;

            switch (chosen.Kind)
            {
                case "page":
                    navBar.Select(chosen.Key);
                    break;
                case "action" when chosen.Key == "theme":
                    Theme.SetDark(!Theme.DarkSetting);
                    break;
                case "action" when chosen.Key == "shortcuts":
                    ShowShortcuts();
                    break;
                case "action" when chosen.Key == "logout":
                    _ = LogoutAsync();
                    break;
                case "chat":
                    ChatPage.PendingConversationId = chosen.Id;
                    navBar.Select("chat");
                    break;
                case "tutor":
                    await ShowTutorAsync(chosen.Id);
                    break;
            }
        }

        private static async Task AddTutorsToSwitcherAsync(QuickSwitcherDialog dialog)
        {
            try
            {
                var tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                if (dialog.IsDisposed) return;

                dialog.AddEntries(tutors.Select(t =>
                    new QuickEntry("tutor", "tutor", t.Id, t.FullName, "Oktató · " + (string.IsNullOrWhiteSpace(t.Subjects) ? "nincs tantárgy" : t.Subjects), "★")));
            }
            catch (ApiException)
            {
                // az oktatók nélkül is használható a gyorskereső
            }
        }

        private async Task ShowTutorAsync(int tutorId)
        {
            try
            {
                var tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                if (tutors.FirstOrDefault(t => t.Id == tutorId) is { } tutor)
                    await TutorActions.ShowProfileAsync(this, tutor, key => navBar.Select(key));
            }
            catch (ApiException ex)
            {
                MessageBox.Show(this, ex.Message, "Oktató profilja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}

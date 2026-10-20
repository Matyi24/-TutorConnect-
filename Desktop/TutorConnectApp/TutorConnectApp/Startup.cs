using Microsoft.Win32;

namespace TutorConnectApp
{
    /// <summary>
    /// Indítás a Windowsszal: a felhasználó saját Run kulcsába (HKCU) kerül az app, rendszergazdai jog nélkül.
    /// Az indítás a --tray kapcsolóval történik, így az app a háttérben, a tálcán indul.
    /// </summary>
    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TutorConnect";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                    return key?.GetValue(ValueName) is string;
                }
                catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
                {
                    return false;
                }
            }
        }

        /// <summary>Bekapcsolja vagy kikapcsolja az automatikus indítást. Igaz, ha sikerült.</summary>
        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
                if (enabled)
                    key.SetValue(ValueName, $"\"{Application.ExecutablePath}\" --tray");
                else
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Ideiglenes fájlok takarítása: a chatből megnyitott csatolmányok és a beillesztett képek a
    /// %TEMP%\TutorConnect mappába kerülnek. Induláskor törli az egy napnál régebbieket, hogy ne halmozódjanak,
    /// és ne maradjon ott felesleges személyes tartalom.
    /// </summary>
    internal static class TempFiles
    {
        public static void CleanOld(TimeSpan olderThan)
        {
            try
            {
                string root = Path.Combine(Path.GetTempPath(), "TutorConnect");
                if (!Directory.Exists(root)) return;

                foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos())
                {
                    if (DateTime.UtcNow - entry.LastWriteTimeUtc < olderThan) continue;

                    try
                    {
                        if (entry is DirectoryInfo directory) directory.Delete(recursive: true);
                        else entry.Delete();
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // használatban van (pl. nyitva egy program): a következő induláskor újra próbáljuk
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // a takarítás nem kritikus
            }
        }
    }
}

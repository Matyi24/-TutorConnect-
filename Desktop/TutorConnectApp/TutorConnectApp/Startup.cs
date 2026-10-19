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
}

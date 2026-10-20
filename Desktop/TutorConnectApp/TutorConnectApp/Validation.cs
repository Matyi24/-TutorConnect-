using System.Text.RegularExpressions;

namespace TutorConnectApp
{
    /// <summary>
    /// A beviteli szabályok egy helyen: ugyanazok, mint a szerveren (így a hiba azonnal kiderül, nem kell a szerverre várni).
    /// </summary>
    internal static class Validation
    {
        private static readonly Regex EmailPattern = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);
        private static readonly Regex StrongPasswordPattern = new(@"^(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{6,}$", RegexOptions.Compiled);

        public const string PasswordRule = "A jelszó legalább 6 karakter legyen, és tartalmazzon számot és speciális karaktert.";

        /// <summary>Érvényes e-mail cím (legfeljebb 100 karakter).</summary>
        public static bool IsEmail(string text) => text.Length <= 100 && EmailPattern.IsMatch(text);

        /// <summary>Legalább 6 karakter, számmal és speciális karakterrel.</summary>
        public static bool IsStrongPassword(string text) => StrongPasswordPattern.IsMatch(text);
    }
}

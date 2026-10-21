using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace TutorConnectApp
{
    /// <summary>
    /// A "Emlékezz rám" funkció: a szerver munkamenet-sütijét menti el a gépen, a Windows saját titkosításával (DPAPI),
    /// így csak ugyanaz a Windows-felhasználó tudja visszaolvasni. A jelszó soha nem kerül lemezre.
    /// </summary>
    internal static class SessionStore
    {
        private sealed class Data
        {
            public string Url { get; set; } = "";
            public string Name { get; set; } = "";
            public string Value { get; set; } = "";
        }

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TutorConnect", "session.dat");

        public static void Save(string url, string cookieName, string cookieValue)
        {
            try
            {
                string json = JsonSerializer.Serialize(new Data { Url = url, Name = cookieName, Value = cookieValue });
                byte[]? sealedBytes = Transform(Encoding.UTF8.GetBytes(json), protect: true);
                if (sealedBytes == null) return;

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllBytes(FilePath, sealedBytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // a mentés nem kritikus: legfeljebb újra be kell jelentkezni
            }
        }

        /// <summary>A mentett süti, ha ugyanahhoz a szerverhez tartozik; egyébként null.</summary>
        public static (string Name, string Value)? Load(string url)
        {
            try
            {
                if (!File.Exists(FilePath)) return null;

                byte[]? plain = Transform(File.ReadAllBytes(FilePath), protect: false);
                if (plain == null) return null;

                var data = JsonSerializer.Deserialize<Data>(Encoding.UTF8.GetString(plain));
                if (data == null || data.Url != url || data.Name.Length == 0 || data.Value.Length == 0) return null;
                return (data.Name, data.Value);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // nem kritikus
            }
        }

        // ------------------------------------------------------------
        // Windows DPAPI (crypt32.dll)
        // ------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Size;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DataBlob dataIn, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr handle);

        private static byte[]? Transform(byte[] input, bool protect)
        {
            var source = new DataBlob { Size = input.Length, Data = Marshal.AllocHGlobal(Math.Max(input.Length, 1)) };
            try
            {
                Marshal.Copy(input, 0, source.Data, input.Length);

                DataBlob target;
                bool ok = protect
                    ? CryptProtectData(ref source, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out target)
                    : CryptUnprotectData(ref source, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out target);
                if (!ok) return null;

                try
                {
                    var result = new byte[target.Size];
                    Marshal.Copy(target.Data, result, 0, result.Length);
                    return result;
                }
                finally
                {
                    LocalFree(target.Data);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(source.Data);
            }
        }
    }
}

using System.IO;
using System.Text;

namespace DRMDetector
{
    public static class DrmScanner
    {
        public const long MAX_FILE_SIZE = 800 * 1024 * 1024;

        /// <summary>Collects the files to scan for a single file or top-level folder target.</summary>
        public static List<string> CollectTargets(string targetPath, bool isFolder)
        {
            if (!isFolder)
            {
                return new List<string> { targetPath };
            }

            var files = new List<string>();
            try
            {
                foreach (string file in Directory.GetFiles(targetPath))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".exe" || ext == ".dll")
                    {
                        files.Add(file);
                    }
                }
            }
            catch { }

            return files;
        }

        public static List<string> Scan(string filePath)
        {
            var detected = new List<string>();

            try
            {
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext != ".exe" && ext != ".dll")
                {
                    return detected;
                }

                byte[] fileBytes = File.ReadAllBytes(filePath);
                string fileText = ExtractReadableStrings(fileBytes);
                string fileName = Path.GetFileName(filePath).ToLower();

                if (fileName.Contains("denuvo") || ContainsExactString(fileText, "denuvo"))
                {
                    detected.Add("Denuvo");
                }

                if (ContainsExactString(fileText, "arxan.dll") || 
                    ContainsExactString(fileText, "arxanapp.dll") || 
                    ContainsExactString(fileText, "arxan_clr.dll") || 
                    fileName.Contains("arxan"))
                {
                    detected.Add("Arxan");
                }

                // A real Steam stub is a small launcher EXE referencing the Steam API.
                // Dummy steam_api*.dlls shipped by non-Steam publishers (Ubisoft, EA, GOG)
                // and DLLs merely using SteamWorks are not evidence of a stub.
                if (ext == ".exe")
                {
                    bool hasStubMarker = fileName.Contains("steamstub") || ContainsExactString(fileText, "steamstub");
                    bool smallExeRefingSteamApi = fileBytes.Length < 5 * 1024 * 1024 &&
                        (ContainsExactString(fileText, "steam_api") || ContainsExactString(fileText, "steamworks"));

                    if (hasStubMarker || smallExeRefingSteamApi)
                    {
                        detected.Add("SteamStub");
                    }
                }

                if (ContainsExactString(fileText, "vmprotect") ||
                    ContainsExactString(fileText, ".vmp0") || 
                    ContainsExactString(fileText, ".vmp1"))
                {
                    detected.Add("VMProtect");
                }

                if (ContainsExactString(fileText, "themida.dll") || 
                    ContainsExactString(fileText, "themida.sys") || 
                    ContainsExactString(fileText, "winlicense.dll") || 
                    ContainsExactString(fileText, ".themida"))
                {
                    detected.Add("Themida");
                }

                if (ContainsExactString(fileText, ".aspack") || 
                    ContainsExactString(fileText, ".adata"))
                {
                    detected.Add("Aspack");
                }

                if (ContainsExactString(fileText, ".upx0") || 
                    ContainsExactString(fileText, ".upx1") || 
                    ContainsExactString(fileText, "upx!"))
                {
                    detected.Add("UPX");
                }

                if (ContainsExactString(fileText, "pec2.exe") || 
                    ContainsExactString(fileText, "pec1.exe") || 
                    ContainsExactString(fileText, "pecompact"))
                {
                    detected.Add("PECompact");
                }

                if (ContainsExactString(fileText, "obsidium"))
                {
                    detected.Add("Obsidium");
                }

                if (ContainsExactString(fileText, "codeveil"))
                {
                    detected.Add("CodeVeil");
                }
            }
            catch { }

            return detected;
        }

        private static string ExtractReadableStrings(byte[] data)
        {
            var sb = new StringBuilder(data.Length / 16);
            const int minLength = 4;

            for (int i = 0; i < data.Length; )
            {
                if (data[i] >= 32 && data[i] <= 126)
                {
                    int start = i;
                    while (i < data.Length && data[i] >= 32 && data[i] <= 126) i++;
                    int len = i - start;
                    if (len >= minLength)
                    {
                        for (int j = 0; j < len; j++) sb.Append((char)data[start + j]);
                        sb.Append('\n');
                    }
                }
                else i++;
            }

            for (int i = 0; i + 1 < data.Length; )
            {
                if ((data[i] >= 32 && data[i] <= 126) && data[i + 1] == 0)
                {
                    int start = i;
                    while (i + 1 < data.Length && data[i] >= 32 && data[i] <= 126 && data[i + 1] == 0) i += 2;
                    int len = (i - start) / 2;
                    if (len >= minLength)
                    {
                        for (int j = 0; j < len; j++) sb.Append((char)data[start + j * 2]);
                        sb.Append('\n');
                    }
                }
                else i++;
            }

            return sb.ToString().ToLowerInvariant();
        }

        private static bool ContainsExactString(string text, string pattern)
        {
            return text.Contains(pattern);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace DRMDetector
{
    public partial class MainWindow : Window
    {
        private string selectedPath = "";
        private bool isFolder = false;
        private const long MAX_FILE_SIZE = 800 * 1024 * 1024;
        private const int MAX_FILES_TO_SCAN = 200;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Executable Files (*.exe;*.dll;*.bin)|*.exe;*.dll;*.bin|All Files (*.*)|*.*",
                Title = "Select Game Executable"
            };

            System.Windows.Forms.DialogResult result = openFileDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                selectedPath = openFileDialog.FileName;
                isFolder = false;
                SelectedPathText.Text = selectedPath;
                ScanButton.IsEnabled = true;
                StatusText.Text = "File selected. Click START SCAN to analyze.";
            }
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                folderDialog.Description = "Select Game Folder";
                folderDialog.ShowNewFolderButton = false;

                System.Windows.Forms.DialogResult result = folderDialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    selectedPath = folderDialog.SelectedPath;
                    isFolder = true;
                    SelectedPathText.Text = selectedPath;
                    ScanButton.IsEnabled = true;
                    StatusText.Text = "Folder selected. Click START SCAN to analyze all executables.";
                }
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            ResultsPanel.Children.Clear();
            ScanButton.IsEnabled = false;
            StatusText.Text = "Scanning...";

            try
            {
                List<string> filesToScan;
                if (isFolder)
                {
                    filesToScan = GetExecutableFiles(selectedPath);
                }
                else
                {
                    filesToScan = new List<string> { selectedPath };
                }

                var allDetectedDRMs = new Dictionary<string, List<string>>();
                int scannedCount = 0;
                int skipCount = 0;

                foreach (string file in filesToScan)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        
                        if (fileInfo.Length > MAX_FILE_SIZE)
                        {
                            skipCount++;
                            continue;
                        }

                        StatusText.Text = $"Scanning {Path.GetFileName(file)} ({scannedCount + 1}/{Math.Min(filesToScan.Count, MAX_FILES_TO_SCAN)})...";
                        
                        var drms = await Task.Run(() => ScanForDRM(file));
                        
                        if (drms.Count > 0)
                        {
                            allDetectedDRMs[file] = drms;
                        }

                        scannedCount++;

                        if (scannedCount >= MAX_FILES_TO_SCAN)
                        {
                            break;
                        }
                    }
                    catch
                    {
                        skipCount++;
                    }
                }

                DisplayResults(allDetectedDRMs, selectedPath, isFolder, scannedCount, skipCount);
                StatusText.Text = $"Scan complete. {scannedCount} file(s) analyzed, {skipCount} skipped.";
            }
            catch (Exception ex)
            {
                AddResultEntry("Error", ex.Message, false);
                StatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                ScanButton.IsEnabled = true;
            }
        }

        private List<string> GetExecutableFiles(string folderPath)
        {
            var files = new List<string>();
            try
            {
                string[] exeFiles = Directory.GetFiles(folderPath, "*.exe", SearchOption.TopDirectoryOnly);
                foreach (string exe in exeFiles)
                {
                    files.Add(exe);
                }

                string[] dllFiles = Directory.GetFiles(folderPath, "*.dll", SearchOption.TopDirectoryOnly);
                foreach (string dll in dllFiles)
                {
                    files.Add(dll);
                }
            }
            catch { }
            return files;
        }

        private List<string> ScanForDRM(string filePath)
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

        private string ExtractReadableStrings(byte[] data)
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

        private bool ContainsExactString(string text, string pattern)
        {
            return text.Contains(pattern);
        }

        private void DisplayResults(Dictionary<string, List<string>> results, string targetPath, bool scanningFolder, int scanned, int skipped)
        {
            ResultsPanel.Children.Clear();

            if (scanningFolder)
            {
                AddHeaderText($"Folder: {Path.GetFileName(targetPath)}");
                AddHeaderText($"{results.Count} file(s) with DRM found", 14, "PrimaryPurple");
                AddHeaderText($"Scanned: {scanned} | Skipped: {skipped}", 11, "TextSecondary");
                AddMargin(10);

                foreach (var kvp in results)
                {
                    string fileName = Path.GetFileName(kvp.Key);
                    AddHeaderText($"File: {fileName}", 13, "TextSecondary");

                    foreach (string drm in kvp.Value)
                    {
                        AddResultEntry(drm, "DETECTED", true);
                    }
                    AddMargin(8);
                }

                if (results.Count == 0)
                {
                    AddResultEntry("No Known DRM", "Not detected", false);
                    AddHeaderText("Note: Some custom or newer DRMs may not be recognized.", 11, "TextSecondary");
                }
            }
            else
            {
                string fileName = Path.GetFileName(targetPath);
                AddHeaderText($"File: {fileName}");

                if (results.ContainsKey(targetPath))
                {
                    foreach (string drm in results[targetPath])
                    {
                        AddResultEntry(drm, "DETECTED", true);
                    }
                }
                else
                {
                    AddResultEntry("No Known DRM", "Not detected", false);
                    AddHeaderText("Note: Some custom or newer DRMs may not be recognized.", 11, "TextSecondary");
                }
            }
        }

        private void AddResultEntry(string drmName, string status, bool detected)
        {
            var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            
            string colorKey = detected ? "SuccessGreen" : "DangerRed";
            var statusText = $"[{status}]";

            var nameText = new TextBlock
            {
                Text = drmName,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Width = 200
            };

            var statusBlock = new TextBlock
            {
                Text = statusText,
                Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources[colorKey],
                FontSize = 13,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };

            panel.Children.Add(nameText);
            panel.Children.Add(statusBlock);
            ResultsPanel.Children.Add(panel);
        }

        private void AddHeaderText(string text, int fontSize = 16, string colorKey = "TextPrimary")
        {
            ResultsPanel.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources[colorKey],
                FontSize = fontSize,
                Margin = new Thickness(0, 6, 0, 6)
            });
        }

        private void AddMargin(int height)
        {
            ResultsPanel.Children.Add(new TextBlock { Height = height });
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DRMDetector
{
    public partial class MainWindow : Window
    {
        private string selectedPath = "";
        private bool isFolder = false;
        private const int MAX_FILES_TO_SCAN = 200;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Executable Files (*.exe;*.dll;*.bin)|*.exe;*.dll;*.bin|All Files (*.*)|*.*",
                Title = "Select Game Executable"
            };

            if (openFileDialog.ShowDialog() == true)
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
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select Game Folder"
            };

            if (folderDialog.ShowDialog() == true)
            {
                selectedPath = folderDialog.FolderName;
                isFolder = true;
                SelectedPathText.Text = selectedPath;
                ScanButton.IsEnabled = true;
                StatusText.Text = "Folder selected. Click START SCAN to analyze all executables.";
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            ResultsPanel.Children.Clear();
            ScanButton.IsEnabled = false;
            StatusText.Text = "Scanning...";

            try
            {
                List<string> filesToScan = DrmScanner.CollectTargets(selectedPath, isFolder);

                var allDetectedDRMs = new Dictionary<string, List<string>>();
                int scannedCount = 0;
                int skipCount = 0;

                foreach (string file in filesToScan)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        
                        if (fileInfo.Length > DrmScanner.MAX_FILE_SIZE)
                        {
                            skipCount++;
                            continue;
                        }

                        StatusText.Text = $"Scanning {Path.GetFileName(file)} ({scannedCount + 1}/{Math.Min(filesToScan.Count, MAX_FILES_TO_SCAN)})...";
                        
                        var drms = await Task.Run(() => DrmScanner.Scan(file));
                        
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
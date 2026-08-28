using System;
using System.IO;
using System.Windows;

namespace DRMDetector
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length >= 1 && string.Equals(e.Args[0], "--scan", StringComparison.OrdinalIgnoreCase))
            {
                Shutdown(RunCliScan(e.Args.Skip(1).ToArray()));
                return;
            }

            base.OnStartup(e);
        }

        private static int RunCliScan(string[] targets)
        {
            if (targets.Length == 0)
            {
                Console.WriteLine("usage: DRMDetector.exe --scan <file-or-folder> [more...]");
                return 2;
            }

            Console.WriteLine("DRM Detector - command line scan");

            int scanned = 0;
            int withDetections = 0;
            int errors = 0;

            foreach (string target in targets)
            {
                bool isFolder = Directory.Exists(target);

                if (!isFolder && !File.Exists(target))
                {
                    Console.WriteLine($"not found: {target}");
                    errors++;
                    continue;
                }

                List<string> files = DrmScanner.CollectTargets(target, isFolder);

                foreach (string file in files)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.Length > DrmScanner.MAX_FILE_SIZE)
                        {
                            Console.WriteLine($"skipped ({info.Length / 1024 / 1024} MB exceeds limit): {file}");
                            continue;
                        }

                        Console.Write($"scanning {Path.GetFileName(file)} ... ");
                        List<string> drms = DrmScanner.Scan(file);
                        scanned++;

                        if (drms.Count > 0)
                        {
                            withDetections++;
                            Console.WriteLine(string.Join(", ", drms));
                        }
                        else
                        {
                            Console.WriteLine("no known DRM detected");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        Console.WriteLine($"error: {ex.Message}");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine($"{scanned} file(s) scanned, {withDetections} with detections.");
            return errors > 0 ? 1 : 0;
        }
    }
}

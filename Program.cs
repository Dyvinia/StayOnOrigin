using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using DyviniaUtils;
using Microsoft.Win32;

namespace StayOnOrigin {
    internal class Program {
        public static string OriginPath => Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Origin")?.GetValue("OriginPath")?.ToString();
        public static Version OriginVersion => new(FileVersionInfo.GetVersionInfo(OriginPath).FileVersion.Replace(",", "."));
        public static string TempFolder => Path.Combine(Environment.CurrentDirectory, "temp");

        static void Main() {
            // Version and Stuff
            Console.WriteLine($"StayOnOrigin v{Assembly.GetEntryAssembly().GetName().Version.ToString()[..5]} by Dyvinia");
            WriteSeparator();

            // Kill All Origin/EA related processes
            KillEA();

            // Check if Origin is too new (Anything after 10.5.120.x)
            if (OriginVersion.CompareTo(new("10.5.120.0")) > 0) {
                Console.WriteLine($"Origin v{OriginVersion} is too recent");
                Console.WriteLine("Downgrading to Origin v10.5.118.52644...");
                UpdateOrigin().Wait();
                WriteSeparator();
            }
            else {
                Console.WriteLine($"Origin v{OriginVersion}");
                WriteSeparator();
            }

            // Delete Origin's internal updater
            RemoveSetupInternal();
            WriteSeparator();

            // Disable EA Desktop migration
            DisableMigration();
            WriteSeparator();

            // End
            ResetTempDir(false);
            Console.WriteLine("Done");
            Console.Write("Press Any Key to Exit...");
            Console.ReadKey();
        }

        static async Task UpdateOrigin() {
            ResetTempDir();

            // Download
            string originURL = @"https://origin-a.akamaihd.net/Origin-Client-Download/origin/live/OriginUpdate_10_5_118_52644.zip";
            string destinationPath = Path.Combine(TempFolder, Path.GetFileName(originURL));

            IProgress<double> progress = new Progress<double>(p => {
                int percentage = Convert.ToInt32(p * 100);
                Console.Write($"\rDownloading: {percentage}%");
            });

            await Downloader.Download(originURL, destinationPath, progress);
            Console.WriteLine();
            Console.WriteLine($"Downloaded {Path.GetFileName(originURL)}");

            // Install
            using ZipArchive archive = ZipFile.OpenRead(destinationPath);
            int i = 0;
            foreach (ZipArchiveEntry entry in archive.Entries) {
                int percentage = Convert.ToInt32(100*i++/(float)archive.Entries.Count);
                Console.Write($"\rInstalling: {percentage}%");
                entry.ExtractToFile(Path.Combine(Path.GetDirectoryName(OriginPath), entry.FullName), true);
            }
            Console.WriteLine();
            Console.WriteLine($"Installed Origin v10.5.118.52644");
        }

        static void RemoveSetupInternal() {
            string originSetupInternal = OriginPath.Replace("Origin.exe", "OriginSetupInternal.exe");
            string originThinSetupInternal = OriginPath.Replace("Origin.exe", "OriginThinSetupInternal.exe");

            if (File.Exists(originSetupInternal)) {
                // Backup file by copying to file.exe.bak
                Console.WriteLine($"Backing up {originSetupInternal}");
                File.Copy(originSetupInternal, originSetupInternal + ".bak", true);

                // Replace the contents of the file with nothing
                Console.WriteLine($"Clearing {originSetupInternal}");
                File.WriteAllText(originSetupInternal, "");
            }

            if (File.Exists(originThinSetupInternal)) {
                // Backup file by copying to file.exe.bak
                Console.WriteLine($"Backing up {originThinSetupInternal}");
                File.Copy(originThinSetupInternal, originThinSetupInternal + ".bak", true);

                // Replace the contents of the file with nothing
                Console.WriteLine($"Clearing {originThinSetupInternal}");
                File.WriteAllText(originThinSetupInternal, "");
            }
        }

        static void DisableMigration() {
            string localXML = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Origin\local.xml");

            Console.WriteLine($"Opening {localXML}");
            string[] fileLines = File.ReadAllLines(localXML).ToArray();
            List<string> fileLinesNew = new();

            string migrationSetting = "  <Setting value=\"true\" key=\"MigrationDisabled\" type=\"1\"/>";
            string updateUrlSetting = "  <Setting key=\"UpdateURL\" value=\"http://blah.blah/\" type=\"10\"/>";
            string autoPatchGlobalSetting = "  <Setting key=\"AutoPatchGlobal\" value=\"true\" type=\"1\"/>";
            string autoUpdateSetting = "  <Setting value=\"true\" key=\"AutoUpdate\" type=\"1\"/>";

            List<string> settingsCheck = new() {
                "</Settings>",
                migrationSetting,
                updateUrlSetting,
                autoPatchGlobalSetting,
                autoUpdateSetting
            };
            foreach (string line in fileLines) {
                if (!settingsCheck.Any(line.Contains))
                    fileLinesNew.Add(line);
            }

            // Add stuff
            fileLinesNew.Add(migrationSetting);
            fileLinesNew.Add(updateUrlSetting);
            fileLinesNew.Add(autoPatchGlobalSetting.Replace("true", "false"));
            fileLinesNew.Add(autoUpdateSetting.Replace("true", "false"));
            fileLinesNew.Add("</Settings>");

            // write new text
            Console.WriteLine($"Writing text to {localXML}");
            File.WriteAllLines(localXML, fileLinesNew.ToArray());
        }

        static void KillEA() {
            bool addSeparator = false;
            foreach (Process process in Process.GetProcessesByName("EADesktop")) {
                process.Kill();
                Console.WriteLine($"Killed {process.ProcessName}");
                addSeparator = true;
            }
            foreach (Process process in Process.GetProcessesByName("Origin")) {
                process.Kill();
                Console.WriteLine($"Killed {process.ProcessName}");
                addSeparator = true;
            }
            foreach (Process process in Process.GetProcessesByName("OriginWebHelperService")) {
                process.Kill();
                Console.WriteLine($"Killed {process.ProcessName}");
                addSeparator = true;
            }
            if (addSeparator)
                WriteSeparator();
        }

        static void ResetTempDir(bool recreateDir = true) {
            if (Directory.Exists(TempFolder))
                Directory.Delete(TempFolder, true);
            if (recreateDir)
                Directory.CreateDirectory(TempFolder);
        }

        static void WriteSeparator() {
            Console.WriteLine(new string('-', Console.WindowWidth - 1));
        }
    }
}
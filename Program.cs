using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using DyviniaUtils;
using Microsoft.Win32;

namespace StayOnOrigin
{
    internal class Program
    {
        public static string OriginPath => Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Origin")?.GetValue("OriginPath")?.ToString();
        public static Version OriginVersion => new(FileVersionInfo.GetVersionInfo(OriginPath).FileVersion.Replace(",", "."));
        public static string TempDirPath => Path.Combine(Environment.CurrentDirectory, "temp");

        static void Main()
        {
            // Version and Stuff
            Console.WriteLine($"StayOnOrigin v{Assembly.GetEntryAssembly().GetName().Version.ToString()[..5]} by Dyvinia");
            WriteSeparator();

            // Check if Origin Exists
            if (!File.Exists(OriginPath)) {
                Console.WriteLine("Origin is not installed or could not be found.");
                Console.WriteLine("Press Y to install Origin, or any other key to exit.");
                ConsoleKeyInfo key = Console.ReadKey();
                if (key.Key != ConsoleKey.Y)
                    Environment.Exit(0);
                Console.WriteLine("\nDownloading Origin...");
                ResetTempDir();
                InstallOrigin().Wait();
                WriteSeparator();
            }

            // Kill All Origin/EA related processes
            KillProcesses();

            // Check if Origin is too new (Anything after 10.5.120.x)
            if (OriginVersion.CompareTo(new("10.5.120.0")) > 0)
            {
                Console.WriteLine($"Origin v{OriginVersion} is too recent");
                Console.WriteLine("Downgrading to Origin v10.5.118.52644...");
                ResetTempDir();
                UpdateOrigin().Wait();
                WriteSeparator();
            }
            else
            {
                Console.WriteLine($"Origin v{OriginVersion}");
                WriteSeparator();
            }

            // Delete Origin's internal updater
            ClearFile(OriginPath.Replace("Origin.exe", "OriginSetupInternal.exe"), false);
            ClearFile(OriginPath.Replace("Origin.exe", "OriginThinSetupInternal.exe"), false);
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

        static async Task InstallOrigin()
        {
            // Download from EA Servers
            //string originURL = @"https://cdn.discordapp.com/attachments/693482239593283694/1086045449191968899/OriginSetup_1.exe";
            string originURL = @"https://download.dm.origin.com/origin/live/OriginSetup.exe";
            string destinationPath = Path.Combine(TempDirPath, Path.GetFileName(originURL));

            IProgress<double> progress = new Progress<double>(p => {
                int percentage = Convert.ToInt32(p * 100);
                Console.Write($"\rDownloading: {percentage}%");
            });

            await Downloader.Download(originURL, destinationPath, progress);
            Console.WriteLine();
            Console.WriteLine($"Downloaded {Path.GetFileName(originURL)}");

            // Install
            var originInstall = Process.Start(destinationPath);
            Console.WriteLine("Origin is being installed...");
            originInstall.WaitForExit();
            Console.WriteLine();
            Console.WriteLine($"Installed Origin");
        }

        static async Task UpdateOrigin()
        {
            // Download from EA Servers
            string originURL = @"https://origin-a.akamaihd.net/Origin-Client-Download/origin/live/OriginUpdate_10_5_118_52644.zip";
            string destinationPath = Path.Combine(TempDirPath, Path.GetFileName(originURL));

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
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                int percentage = Convert.ToInt32(100 * i++ / (float)archive.Entries.Count);
                Console.Write($"\rInstalling: {percentage}%");
                entry.ExtractToFile(Path.Combine(Path.GetDirectoryName(OriginPath), entry.FullName), true);
            }
            Console.WriteLine();
            Console.WriteLine($"Installed Origin v10.5.118.52644");
        }

        static void ClearFile(string path, bool readOnly)
        {
            if (File.Exists(path) && !File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
            {
                // Backup file by copying to file.exe.bak
                Console.WriteLine($"Backing up {path}");
                File.Copy(path, path + ".bak", true);

                // Replace the contents of the file with nothing
                Console.WriteLine($"Clearing {path}");
                File.WriteAllText(path, "");

                // Set the file to read-only
                if (readOnly)
                    File.SetAttributes(path, FileAttributes.ReadOnly);
            }
        }

        static void DisableMigration()
        {
            string localXML = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Origin\local.xml");

            Console.WriteLine($"Opening {localXML}");
            string[] fileLines = File.ReadAllLines(localXML).ToArray();

            List<string> fileLinesNew = new();
            List<string> settingsCheck = new() {
                "MigrationDisabled",
                "UpdateURL",
                "AutoPatchGlobal",
                "AutoUpdate",
                "/Settings"
            };
            foreach (string line in fileLines)
            {
                if (!settingsCheck.Any(line.Contains))
                    fileLinesNew.Add(line);
            }

            // Add new settings
            fileLinesNew.Add("  <Setting key=\"MigrationDisabled\" value=\"true\" type=\"1\"/>");
            fileLinesNew.Add("  <Setting key=\"UpdateURL\" value=\"http://blah.blah/\" type=\"10\"/>");
            fileLinesNew.Add("  <Setting key=\"AutoPatchGlobal\" value=\"false\" type=\"1\"/>");
            fileLinesNew.Add("  <Setting key=\"AutoUpdate\" value=\"false\" type=\"1\"/>");
            fileLinesNew.Add("</Settings>");

            // write new text
            Console.WriteLine($"Writing text to {localXML}");
            File.WriteAllLines(localXML, fileLinesNew.ToArray());
        }

        static void KillProcesses()
        {
            bool addSeparator = false;
            foreach (Process process in Process.GetProcessesByName("EADesktop"))
            {
                process.Kill();
                Console.WriteLine($"Killed {process.ProcessName}");
                addSeparator = true;
            }
            foreach (Process process in Process.GetProcessesByName("Origin"))
            {
                process.Kill();
                Console.WriteLine($"Killed {process.ProcessName}");
                addSeparator = true;
            }
            foreach (Process process in Process.GetProcessesByName("OriginWebHelperService"))
            {
                process.Kill();
                Console.WriteLine($"Killed {process.ProcessName}");
                addSeparator = true;
            }
            if (addSeparator)
                WriteSeparator(); // Prevent double separator from appearing in console
        }

        static void ResetTempDir(bool recreateDir = true)
        {
            if (Directory.Exists(TempDirPath))
                Directory.Delete(TempDirPath, true);
            if (recreateDir)
                Directory.CreateDirectory(TempDirPath);
        }

        static void WriteSeparator()
        {
            Console.WriteLine(new string('-', Console.WindowWidth - 1));
        }
    }
}

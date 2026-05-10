using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows_Auto_Unzipper.Properties;

namespace Windows_Auto_Unzipper
{
    /// <summary>
    /// Uses FileSystemWatcher to watch for new zip files added to a specified directory
    /// </summary>
    class FolderWatcher : IDisposable
    {
        private UnzipperContext context;
        private FileSystemWatcher watcher;
        private readonly ConcurrentDictionary<string, byte> activeExtractions = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes the FileSystemWatcher
        /// </summary>
        /// <param name="context">Reference to the applications context</param>
        public FolderWatcher(UnzipperContext context)
        {
            this.context = context;

            //Create the FileSystemWatcher
            this.watcher = new FileSystemWatcher();
            if (Directory.Exists(context.GetTargetFolder()))
            {
                this.watcher.Path = context.GetTargetFolder();
            }
            this.watcher.NotifyFilter = NotifyFilters.Attributes
                                | NotifyFilters.CreationTime
                                | NotifyFilters.DirectoryName
                                | NotifyFilters.FileName
                                | NotifyFilters.LastAccess
                                | NotifyFilters.LastWrite
                                | NotifyFilters.Security
                                | NotifyFilters.Size;

            //Set event handlers for FileSystemWatcher
            this.watcher.Created += this.OnCreated;
            this.watcher.Renamed += this.OnRenamed;
            this.watcher.Error += OnError;

            this.watcher.Filter = "*.*";
            this.watcher.IncludeSubdirectories = false;
        }

        /// <summary>
        /// Set which folder the FileSytemWatcher is watching
        /// </summary>
        /// <param name="targetDirectory">Path to the directory</param>
        public void SetTargetDirectory(String targetDirectory)
        {
            this.watcher.EnableRaisingEvents = false;
            if (Directory.Exists(targetDirectory))
            {
                this.watcher.Path = targetDirectory;
            }
        }

        /// <summary>
        /// Starts the FileSystemWatcher if a directory has been set
        /// </summary>
        /// <returns>Returns false if no directory has been set</returns>
        public bool Start()
        {
            if (!String.IsNullOrEmpty(this.watcher.Path) && Directory.Exists(this.context.GetTargetFolder()) && PathsEqual(this.watcher.Path, this.context.GetTargetFolder()))
            {
                this.watcher.EnableRaisingEvents = true;
                Settings.Default.LastRunningMode = "Running";
                Settings.Default.Save();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Stop the FileSystemWatcher
        /// </summary>
        public void Stop()
        {
            this.watcher.EnableRaisingEvents = false;
            Settings.Default.LastRunningMode = "Stopped";
            Settings.Default.Save();
        }

        /// <summary>
        /// Check if the FileSystemWatcher is running
        /// </summary>
        /// <returns>Returns true if running</returns>
        public bool IsRunning()
        {
            return !String.IsNullOrEmpty(this.watcher.Path)
                && Directory.Exists(this.context.GetTargetFolder())
                && PathsEqual(this.watcher.Path, this.context.GetTargetFolder())
                && this.watcher.EnableRaisingEvents;
        }

        /// <summary>
        /// Event handler that is called when a new zip file is added to the directory that is being watched and extracts it
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            this.QueueExtraction(e.FullPath);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            this.QueueExtraction(e.FullPath);
        }

        private void QueueExtraction(string archivePath)
        {
            if (!this.IsSupportedArchive(archivePath))
            {
                return;
            }

            string normalizedPath = Path.GetFullPath(archivePath);
            if (!this.activeExtractions.TryAdd(normalizedPath, 0))
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    string extractDir = GetAvailableExtractDirectory(normalizedPath);
                    ExtractionResult result = Unzipper.Unzip(normalizedPath, extractDir, Settings.Default.AutoDelete);
                    this.context.ReportExtractionResult(result);
                }
                finally
                {
                    this.activeExtractions.TryRemove(normalizedPath, out _);
                }
            });
        }

        private bool IsSupportedArchive(string path)
        {
            string extension = Path.GetExtension(path);
            return ArchiveExtensionSettings
                .Parse(Settings.Default.ArchiveExtensions)
                .Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private static string GetAvailableExtractDirectory(string archivePath)
        {
            string parentDir = Path.GetDirectoryName(archivePath);
            string baseName = Path.GetFileNameWithoutExtension(archivePath);
            string candidate = Path.Combine(parentDir, baseName);
            int suffix = 2;

            while (Directory.Exists(candidate) || File.Exists(candidate))
            {
                candidate = Path.Combine(parentDir, $"{baseName} ({suffix})");
                suffix++;
            }

            return candidate;
        }

        private static bool PathsEqual(string left, string right)
        {
            string normalizedLeft = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
            string normalizedRight = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
            return String.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }


        //Event handler that is called when an error occurs with the FileSystemWatcher
        private static void OnError(object sender, ErrorEventArgs e)
        {
            PrintException(e.GetException());
        }

        private static void PrintException(Exception ex)
        {
            if (ex != null)
            {
                Debug.WriteLine($"Message: {ex.Message}");
                Debug.WriteLine("Stacktrace:");
                Debug.WriteLine(ex.StackTrace);
                Debug.WriteLine("");
                PrintException(ex.InnerException);
            }
        }

        /// <summary>
        /// Dispose of the FileSystemWatcher
        /// </summary>
        public void Dispose()
        {
            this.watcher.Dispose();
        }
    }
}

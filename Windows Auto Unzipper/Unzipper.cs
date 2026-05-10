using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Windows_Auto_Unzipper
{
    public sealed class ExtractionResult
    {
        public ExtractionResult(bool success, string archivePath, string extractDir, string message)
        {
            Success = success;
            ArchivePath = archivePath;
            ExtractDir = extractDir;
            Message = message;
        }

        public bool Success { get; }

        public string ArchivePath { get; }

        public string ExtractDir { get; }

        public string Message { get; }
    }

    /// <summary>
    /// Used to unzip/extract zip files
    /// </summary>
    class Unzipper
    {
        /// <summary>
        /// Unzips/extracts a zip file to a directory and optionally deletes the zip file
        /// </summary>
        /// <param name="fullPath">The location of the source archive</param>
        /// <param name="extractDir">The location the archive will be extracted to</param>
        /// <param name="deleteWhenDone">Should the source archive be deleted after it has been unzipped</param>
        /// <returns>Returns a result with the final status and message.</returns>
        public static ExtractionResult Unzip(string fullPath, string extractDir, bool deleteWhenDone)
        {
            try
            {
                if (!WaitForFileReady(fullPath, true))
                {
                    return Failure(fullPath, extractDir, "Archive is still in use or no longer exists.");
                }

                Directory.CreateDirectory(extractDir);

                string extension = Path.GetExtension(fullPath);
                if (String.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ZipFile.ExtractToDirectory(fullPath, extractDir, false);
                }
                else
                {
                    ExtractWithSharpCompress(fullPath, extractDir);
                }

                if (deleteWhenDone)
                {
                    File.Delete(fullPath);
                }

                RefreshWindowsExplorer();
                string deleteMessage = deleteWhenDone ? " Source archive deleted." : String.Empty;
                return new ExtractionResult(true, fullPath, extractDir, $"Extracted to {extractDir}.{deleteMessage}");
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                return Failure(fullPath, extractDir, ex.Message);
            }
            catch (Exception ex)
            {
                return Failure(fullPath, extractDir, ex.Message);
            }
        }

        /// <summary>
        /// Checks if a file is closed
        /// </summary>
        /// <param name="filepath">The path of the file to check</param>
        /// <param name="wait">If true, wait for a short delay and try again</param>
        /// <returns>Returns true if file is closed</returns>
        public static bool WaitForFileReady(string filepath, bool wait)
        {
            bool fileClosed = false;
            int retries = 20;
            const int delay = 500; // Max time spent here = retries*delay milliseconds
            long lastLength = -1;

            if (!File.Exists(filepath))
                return false;

            do
            {
                try
                {
                    // Attempts to open then close the file in RW mode, denying other users to place any locks.
                    using FileStream fs = File.Open(filepath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    long currentLength = fs.Length;
                    fileClosed = currentLength > 0 && currentLength == lastLength;
                    lastLength = currentLength;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }

                if (!wait) break;

                retries--;

                if (!fileClosed)
                    Thread.Sleep(delay);
            }
            while (!fileClosed && retries > 0);

            return fileClosed;
        }

        public static bool IsFileClosed(string filepath, bool wait)
        {
            return WaitForFileReady(filepath, wait);
        }

        private static void ExtractWithSharpCompress(string fullPath, string extractDir)
        {
            using IArchive archive = ArchiveFactory.Open(fullPath);
            foreach (IArchiveEntry entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(extractDir, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = false
                });
            }
        }

        private static ExtractionResult Failure(string fullPath, string extractDir, string message)
        {
            return new ExtractionResult(false, fullPath, extractDir, $"Failed to extract {Path.GetFileName(fullPath)}: {message}");
        }

        /// <summary>
        /// Refresh any open windows explorer windows to show changes in directory
        /// </summary>
        private static void RefreshWindowsExplorer()
        {
            SHChangeNotify(0x8000000, 0x1000, IntPtr.Zero, IntPtr.Zero);
        }

        [DllImport("Shell32.dll")]
        private static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
        
    }
}

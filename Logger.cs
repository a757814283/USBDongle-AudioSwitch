using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace USBDongle_AudioSwitch
{
    /// <summary>
    /// Thread-safe append-only log with the same 5 MB rotation the original
    /// PowerShell script used: once the file reaches the limit it is moved aside
    /// to "&lt;name&gt;.old" (replacing any previous archive) and a fresh file starts.
    ///
    /// Every write is best-effort. A logging failure must never take down the
    /// monitor loop, so all I/O errors are swallowed.
    /// </summary>
    internal static class Logger
    {
        public const long MaxLogBytes = 5L * 1024 * 1024;

        private static readonly object Gate = new object();

        /// <summary>Raised after a line is written. May fire on any thread.</summary>
        public static event Action<string> LineWritten;

        private static string _logPath;

        public static string LogPath
        {
            get { return _logPath ?? (_logPath = AppPaths.LogFile); }
            set { _logPath = value; }
        }

        public static string ArchivePath
        {
            get { return LogPath + ".old"; }
        }

        public static void Info(string message)
        {
            Write(message);
        }

        public static void Warn(string message)
        {
            Write("WARN: " + message);
        }

        public static void Error(string message)
        {
            Write("ERROR: " + message);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR: " + message + " - " + (ex == null ? "(null)" : ex.GetType().Name + ": " + ex.Message));
        }

        public static void Write(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                          + " - " + message;

            lock (Gate)
            {
                try
                {
                    AppPaths.EnsureConfigDirectory();
                    RotateIfNeeded();
                    File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception)
                {
                    // Disk full, permissions, another process holding the file -
                    // none of these justify interrupting audio switching.
                }
            }

            Action<string> handler = LineWritten;
            if (handler != null)
            {
                try
                {
                    handler(line);
                }
                catch (Exception)
                {
                    // A misbehaving subscriber must not break the log call.
                }
            }
        }

        private static void RotateIfNeeded()
        {
            FileInfo info = new FileInfo(LogPath);
            if (!info.Exists || info.Length < MaxLogBytes)
            {
                return;
            }

            if (File.Exists(ArchivePath))
            {
                File.Delete(ArchivePath);
            }
            File.Move(LogPath, ArchivePath);
        }

        /// <summary>Truncates the active log. Called from the UI's "clear" button.</summary>
        public static void Clear()
        {
            lock (Gate)
            {
                try
                {
                    if (File.Exists(LogPath))
                    {
                        File.Delete(LogPath);
                    }
                }
                catch (Exception)
                {
                }
            }
        }
    }
}

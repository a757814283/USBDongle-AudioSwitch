using System;
using System.IO;

namespace USBDongle_AudioSwitch
{
    /// <summary>
    /// Single source of truth for where the program keeps its files.
    ///
    /// Everything lives under %APPDATA% rather than next to the executable: the
    /// app installs itself into the per-user Run key, and a copy placed under
    /// Program Files would have no write access to its own directory.
    /// </summary>
    internal static class AppPaths
    {
        public const string ProductName = "USBDongle_AudioSwitch";

        public static string ConfigDirectory
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(root, ProductName);
            }
        }

        public static string ConfigFile
        {
            get { return Path.Combine(ConfigDirectory, "config.json"); }
        }

        public static string LogFile
        {
            get { return Path.Combine(ConfigDirectory, "audio_switch.log"); }
        }

        /// <summary>Creates the data directory if needed. Returns false if it is unusable.</summary>
        public static bool EnsureConfigDirectory()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

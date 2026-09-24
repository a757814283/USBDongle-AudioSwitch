using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace USBDongle_AudioSwitch
{
    /// <summary>
    /// Registers the program for start-up via the per-user Run key.
    ///
    /// HKCU is used deliberately instead of HKLM or Task Scheduler: it needs no
    /// elevation, and the audio endpoints being switched are per-user settings
    /// anyway, so a machine-wide entry would be the wrong scope.
    /// </summary>
    internal static class AutoStartManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "USBDongle_AudioSwitch";

        /// <summary>Command line written to the Run key: start straight to the tray.</summary>
        public static string CommandLine
        {
            get { return "\"" + Application.ExecutablePath + "\" " + Program.TrayArgument; }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    object value = key.GetValue(ValueName);
                    return value != null && !string.IsNullOrEmpty(value.ToString());
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// True when the Run entry exists but points somewhere else - typically
        /// because the executable was moved. The UI shows this as enabled but
        /// offers to repair it.
        /// </summary>
        public static bool IsStale()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    object value = key.GetValue(ValueName);
                    if (value == null)
                    {
                        return false;
                    }
                    return !string.Equals(value.ToString(), CommandLine, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Returns null on success, or a message describing the failure.</summary>
        public static string SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                    {
                        return Loc.T(Str.ErrorRegistryKeyOpenFailed, RunKeyPath);
                    }

                    if (enabled)
                    {
                        key.SetValue(ValueName, CommandLine, RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}

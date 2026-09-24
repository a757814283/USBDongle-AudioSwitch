using System;
using Microsoft.Win32;

namespace USBDongle_AudioSwitch
{
    /// <summary>
    /// How the shell is currently drawing the taskbar.
    ///
    /// The notification area is composited straight over the taskbar, so a tray
    /// icon has no plate to carry its contrast the way the window icon does (see
    /// AppIcons) - it has to be drawn in the ink colour the taskbar is not. Which
    /// of the two Windows is using is a per-user setting, and the shell reads it
    /// from the "Personalize" key this class reads.
    ///
    /// It is *System*UsesLightTheme, not AppsUseLightTheme: Windows lets the two
    /// differ (light taskbar with dark app windows, or the reverse), and it is
    /// the system one that decides the taskbar and the notification area.
    /// </summary>
    internal static class SystemTheme
    {
        private const string PersonalizeKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        private const string LightTaskbarValueName = "SystemUsesLightTheme";

        /// <summary>
        /// True when the taskbar is drawn light, so the tray icon needs black ink.
        ///
        /// Read on every call rather than cached: the caller uses it to decide
        /// whether the icon it is showing is still the right one, and a cached
        /// answer could never change.
        /// </summary>
        public static bool UsesLightTaskbar
        {
            get
            {
                object value = ReadValue();
                return value != null && AsInt(value) != 0;
            }
        }

        private static object ReadValue()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, false))
                {
                    return key == null ? null : key.GetValue(LightTaskbarValueName);
                }
            }
            catch (Exception ex)
            {
                // A locked-down or redirected hive is not worth failing over:
                // answering "dark" keeps the white icon, which is what this
                // program drew unconditionally before, and what the taskbar
                // Windows ships by default wants.
                Logger.Warn(Loc.T(Str.LogThemeReadFailed, ex.Message));
                return null;
            }
        }

        /// <summary>
        /// The setting is a REG_DWORD, but a hand-edited or imported hive can
        /// hold it as a string, and a value that cannot be read should not
        /// decide the icon by throwing.
        /// </summary>
        private static int AsInt(object value)
        {
            if (value is int)
            {
                return (int)value;
            }

            int parsed;
            return int.TryParse(Convert.ToString(value), out parsed) ? parsed : 0;
        }
    }
}

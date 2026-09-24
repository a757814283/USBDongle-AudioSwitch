using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using USBDongle_AudioSwitch.Interop;

namespace USBDongle_AudioSwitch
{
    /// <summary>
    /// Loads the icon assets embedded in the executable.
    ///
    /// Three files, because the two places the icon appears need opposite things
    /// from it:
    ///
    ///   app.ico         black line art on an opaque white plate. The plate is
    ///                   what carries the contrast, so the same file reads as a
    ///                   white icon on the dark taskbar Windows 11 ships and
    ///                   blends into a light one, leaving only the black
    ///                   drawing. No theme detection is needed.
    ///
    ///   tray-white.ico  the same drawing as white line art on transparency, for
    ///   tray-black.ico  the notification area on a dark and on a light taskbar
    ///                   respectively. The notification area is composited over
    ///                   the taskbar and needs real alpha - an opaque plate would
    ///                   sit in the tray as a white square - so unlike app.ico
    ///                   there is no plate to lend the drawing its contrast.
    ///                   The ink itself has to follow the theme instead, which is
    ///                   why there are two files and why this class has to ask
    ///                   SystemTheme which one is in force.
    ///
    /// All three are multi-resolution and carry hand-drawn artwork for small
    /// sizes: the original drawing's ear-cup detail collapses into an unreadable
    /// blob below about 40px. Asking for an explicit size is what selects those
    /// frames instead of shrinking the large artwork.
    /// </summary>
    internal static class AppIcons
    {
        private const string TrayOnDarkResource = "tray-white.ico";
        private const string TrayOnLightResource = "tray-black.ico";
        private const string ApplicationResource = "app.ico";

        /// <summary>Sizes the tray icon is ever asked for, smallest first.</summary>
        private static readonly int[] TraySizes = { 16, 20, 24, 32, 40, 48 };

        private static Icon _trayOnDark;
        private static Icon _trayOnLight;
        private static Icon _application;

        /// <summary>
        /// The artwork to sit in the notification area right now: white ink on a
        /// dark taskbar, black ink on a light one.
        ///
        /// The theme is re-read on every call so a caller that has just been told
        /// it changed gets the new icon; the icons themselves are cached per
        /// theme, so a call costs a registry read, not a decode. Comparing the
        /// result against the icon already on screen is how the caller tells
        /// whether anything actually moved.
        ///
        /// Each icon is sized against the display's *real* DPI rather than the
        /// 16x16 this process is told about; see <see cref="TrayIconSize"/>.
        /// </summary>
        public static Icon Tray
        {
            get
            {
                return SystemTheme.UsesLightTaskbar ? OnLightTaskbar : OnDarkTaskbar;
            }
        }

        /// <summary>White line art, for the dark taskbar Windows ships by default.</summary>
        private static Icon OnDarkTaskbar
        {
            get
            {
                return _trayOnDark ??
                       (_trayOnDark = LoadTrayIcon(TrayOnDarkResource, lightTaskbar: false));
            }
        }

        /// <summary>Black line art, for a light ("white theme") taskbar.</summary>
        private static Icon OnLightTaskbar
        {
            get
            {
                return _trayOnLight ??
                       (_trayOnLight = LoadTrayIcon(TrayOnLightResource, lightTaskbar: true));
            }
        }

        /// <summary>
        /// Black artwork on an opaque white plate, for the window, its taskbar
        /// button, Alt-Tab and Explorer.
        /// </summary>
        public static Icon Application
        {
            get
            {
                return _application ??
                       (_application = Load(ApplicationResource, SystemInformation.IconSize));
            }
        }

        /// <summary>
        /// One of the two tray files, at the frame the notification area needs.
        ///
        /// Logs the theme only when the artwork is actually decoded, so the log
        /// says which ink colour is in use without repeating itself every time
        /// the caller re-checks.
        /// </summary>
        private static Icon LoadTrayIcon(string resource, bool lightTaskbar)
        {
            int size = TrayIconSize();

            Logger.Info(Loc.T(lightTaskbar
                ? Str.LogTrayIconLightTaskbar
                : Str.LogTrayIconDarkTaskbar));

            // Smallest frame that still covers the slot, so the shell either
            // draws it 1:1 or scales it down slightly - never up. Sizes <=32
            // hold the hand-composed glyph and >=40 the original line art, so
            // overshooting to a big frame would also swap in artwork that is
            // far too fine to survive being scaled to tray size.
            foreach (int candidate in TraySizes)
            {
                if (candidate >= size)
                {
                    Logger.Info(Loc.T(Str.LogTrayIconFrame, size, candidate));
                    return Load(resource, new Size(candidate, candidate));
                }
            }

            int largest = TraySizes[TraySizes.Length - 1];
            Logger.Info(Loc.T(Str.LogTrayIconFrame, size, largest));
            return Load(resource, new Size(largest, largest));
        }

        /// <summary>
        /// The notification area's icon slot, in real pixels.
        ///
        /// This process is DPI-unaware (see MainForm.Designer.cs), and an unaware
        /// thread is told every display runs at 96 DPI - so
        /// SystemInformation.SmallIconSize answers 16x16 even on a 150% display
        /// whose tray slot is really 24x24, and the shell ends up upscaling a
        /// 16px icon. Reading the true system DPI needs the thread to be
        /// DPI-aware for the duration of the call; the context is restored
        /// immediately afterwards so nothing else about the process changes.
        ///
        /// Falls back to the unaware answer if the API is missing (Windows 7/8)
        /// or the measurement looks implausible.
        /// </summary>
        private static int TrayIconSize()
        {
            int unaware = SystemInformation.SmallIconSize.Width;

            int dpi = SystemDpi();
            if (dpi <= 0)
            {
                return unaware;
            }

            int scaled = (int)Math.Round(unaware * (dpi / 96.0), MidpointRounding.AwayFromZero);

            // SystemDpi already rejects implausible values, so this only guards
            // the arithmetic: a tray icon is never smaller than 16px, and
            // TrayIconSize's caller caps the upper end by frame availability.
            return scaled < 16 ? 16 : scaled;
        }

        /// <summary>True system DPI, or 0 when it cannot be determined.</summary>
        private static int SystemDpi()
        {
            IntPtr previous = IntPtr.Zero;
            bool switched = false;

            try
            {
                previous = Win32.SetThreadDpiAwarenessContext(
                    Win32.DPI_AWARENESS_CONTEXT_SYSTEM_AWARE);
                switched = true;

                uint dpi = Win32.GetDpiForSystem();
                return dpi >= 96 && dpi <= 960 ? (int)dpi : 0;
            }
            catch (EntryPointNotFoundException)
            {
                // Windows 7/8: the API does not exist. The unaware answer stands.
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Warn(Loc.T(Str.LogDpiReadFailed, ex.Message));
                return 0;
            }
            finally
            {
                if (switched)
                {
                    try
                    {
                        Win32.SetThreadDpiAwarenessContext(previous);
                    }
                    catch (Exception)
                    {
                        // Restoring is best-effort; the thread is short-lived anyway.
                    }
                }
            }
        }

        /// <summary>
        /// Reads the frame closest to <paramref name="size"/> from the named
        /// embedded .ico. Falls back to the stock Windows icon rather than
        /// throwing - a missing icon should never stop the program starting.
        /// </summary>
        private static Icon Load(string fileName, Size size)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();

                // The logical name is prefixed with the root namespace, so
                // match on the tail rather than rebuilding it by hand.
                foreach (string name in assembly.GetManifestResourceNames())
                {
                    if (!name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using (Stream stream = assembly.GetManifestResourceStream(name))
                    {
                        if (stream != null)
                        {
                            return new Icon(stream, size);
                        }
                    }
                }

                Logger.Warn(Loc.T(Str.LogIconResourceMissing, fileName));
            }
            catch (Exception ex)
            {
                Logger.Error(Loc.T(Str.LogIconLoadFailed, fileName), ex);
            }

            return SystemIcons.Application;
        }
    }
}

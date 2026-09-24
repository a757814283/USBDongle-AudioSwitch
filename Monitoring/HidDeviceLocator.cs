using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using USBDongle_AudioSwitch.Interop;

namespace USBDongle_AudioSwitch.Monitoring
{
    /// <summary>
    /// Resolves the receiver's HID collection to a device path.
    ///
    /// The path is never hard-coded. A HID interface path looks like
    ///   \\?\hid#vid_413c&amp;pid_a529&amp;mi_05&amp;col02#9&amp;2658857&amp;0&amp;0001#{4d1e55b2-...}
    /// and the "#9&amp;2658857&amp;0&amp;0001" segment encodes the physical USB port, so
    /// moving the dongle to another socket changes it. Only the stable half
    /// (VID/PID/MI/COL) is configured; the port half is discovered at runtime.
    /// </summary>
    internal static class HidDeviceLocator
    {
        /// <summary>GUID_DEVINTERFACE_HID.</summary>
        public static readonly Guid HidInterfaceClassGuid =
            new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");

        private const int InstanceIdBufferSize = 512;

        /// <summary>
        /// Returns the device path of the first HID collection whose instance ID
        /// contains <paramref name="match"/>, or null when nothing matches.
        /// </summary>
        public static string FindDevicePath(string match)
        {
            if (string.IsNullOrWhiteSpace(match))
            {
                return null;
            }

            foreach (string instanceId in EnumerateInstanceIds())
            {
                if (instanceId.IndexOf(match, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                string path = GetInterfacePath(instanceId);
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Every HID collection currently present. Surfaced in the UI so a wrong
        /// match string can be diagnosed without reaching for Device Manager.
        /// </summary>
        public static List<string> EnumerateInstanceIds()
        {
            List<string> results = new List<string>();
            Guid guid = HidInterfaceClassGuid;

            IntPtr deviceInfoSet = SetupApi.SetupDiGetClassDevsW(
                ref guid, IntPtr.Zero, IntPtr.Zero,
                SetupApi.DIGCF_PRESENT | SetupApi.DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == SetupApi.InvalidHandleValue)
            {
                return results;
            }

            try
            {
                for (int index = 0; ; index++)
                {
                    SetupApi.SP_DEVICE_INTERFACE_DATA interfaceData = SetupApi.SP_DEVICE_INTERFACE_DATA.Create();

                    if (!SetupApi.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref guid, index,
                            ref interfaceData))
                    {
                        // ERROR_NO_MORE_ITEMS ends the enumeration; anything else
                        // means the list is unusable, so stop either way.
                        break;
                    }

                    SetupApi.SP_DEVINFO_DATA infoData = SetupApi.SP_DEVINFO_DATA.Create();
                    int required;
                    // First call sizes the buffer and, as a side effect, fills in infoData.
                    SetupApi.SetupDiGetDeviceInterfaceDetailW(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0,
                        out required, ref infoData);

                    string instanceId = ReadInstanceId(deviceInfoSet, ref infoData);
                    if (!string.IsNullOrEmpty(instanceId))
                    {
                        results.Add(instanceId);
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                SetupApi.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return results;
        }

        /// <summary>
        /// Asks SetupAPI for the canonical interface path, falling back to
        /// rebuilding it from the instance ID - the same transformation the
        /// original PowerShell script performed.
        /// </summary>
        private static string GetInterfacePath(string instanceId)
        {
            Guid guid = HidInterfaceClassGuid;

            IntPtr deviceInfoSet = SetupApi.SetupDiGetClassDevsW(
                ref guid, IntPtr.Zero, IntPtr.Zero,
                SetupApi.DIGCF_PRESENT | SetupApi.DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == SetupApi.InvalidHandleValue)
            {
                return BuildPathFromInstanceId(instanceId);
            }

            try
            {
                for (int index = 0; ; index++)
                {
                    SetupApi.SP_DEVICE_INTERFACE_DATA interfaceData = SetupApi.SP_DEVICE_INTERFACE_DATA.Create();

                    if (!SetupApi.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref guid, index,
                            ref interfaceData))
                    {
                        break;
                    }

                    SetupApi.SP_DEVINFO_DATA infoData = SetupApi.SP_DEVINFO_DATA.Create();
                    int required;
                    SetupApi.SetupDiGetDeviceInterfaceDetailW(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0,
                        out required, ref infoData);

                    string candidate = ReadInstanceId(deviceInfoSet, ref infoData);
                    if (!string.Equals(candidate, instanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string path = ReadDetailPath(deviceInfoSet, ref interfaceData, ref infoData, required);
                    return string.IsNullOrEmpty(path) ? BuildPathFromInstanceId(instanceId) : path;
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                SetupApi.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return BuildPathFromInstanceId(instanceId);
        }

        private static string ReadDetailPath(IntPtr deviceInfoSet,
            ref SetupApi.SP_DEVICE_INTERFACE_DATA interfaceData,
            ref SetupApi.SP_DEVINFO_DATA infoData, int requiredSize)
        {
            if (requiredSize <= 0)
            {
                return null;
            }

            // The struct is a DWORD followed by a variable-length string. cbSize
            // is 8 on 64-bit and 6 on 32-bit; the path itself always starts at
            // offset 4 because the native alignment is 4 on both.
            int cbSize = IntPtr.Size == 8 ? 8 : 6;
            int bufferSize = requiredSize + cbSize + SetupApi.MaxDevicePath;

            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                Marshal.WriteInt32(buffer, cbSize);

                int ignored;
                if (!SetupApi.SetupDiGetDeviceInterfaceDetailW(deviceInfoSet, ref interfaceData, buffer,
                        bufferSize, out ignored, ref infoData))
                {
                    return null;
                }

                return Marshal.PtrToStringUni(new IntPtr(buffer.ToInt64() + 4));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static string ReadInstanceId(IntPtr deviceInfoSet, ref SetupApi.SP_DEVINFO_DATA infoData)
        {
            if (infoData.cbSize == 0)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder(InstanceIdBufferSize);
            int required;
            if (!SetupApi.SetupDiGetDeviceInstanceIdW(deviceInfoSet, ref infoData, builder,
                    builder.Capacity, out required))
            {
                return null;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Mirrors the original script: swap the instance-ID separators for '#',
        /// lowercase, and append the HID interface GUID.
        /// </summary>
        private static string BuildPathFromInstanceId(string instanceId)
        {
            return @"\\?\" + instanceId.Replace('\\', '#').ToLowerInvariant()
                   + "#" + HidInterfaceClassGuid.ToString("B").ToLowerInvariant();
        }
    }
}

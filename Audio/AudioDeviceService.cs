using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using USBDongle_AudioSwitch.Interop;

namespace USBDongle_AudioSwitch.Audio
{
    /// <summary>
    /// Reads the render endpoints from the Windows audio stack. Playback only -
    /// the program never switches recording devices.
    /// </summary>
    internal static class AudioDeviceService
    {
        private const int VT_LPWSTR = 31;
        private const int DEVICE_STATE_ACTIVE = 0x01;

        // {a45c254e-df1c-4efd-8020-67d146a850e0}, 14  =>  PKEY_Device_FriendlyName
        private static readonly PROPERTYKEY PkeyDeviceFriendlyName = new PROPERTYKEY
        {
            fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
            pid = 14
        };

        /// <summary>
        /// All active playback endpoints. Returns an empty list rather than
        /// throwing - the UI calls this on a timer and during startup, and a
        /// transient audio-service hiccup should just show an empty list.
        /// </summary>
        public static List<AudioDeviceInfo> GetPlaybackDevices()
        {
            List<AudioDeviceInfo> devices = new List<AudioDeviceInfo>();

            using (ComApartment.Enter())
            {
                IMMDeviceEnumerator enumerator = null;
                IMMDeviceCollection collection = null;
                try
                {
                    enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

                    int hr = enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE_ACTIVE, out collection);
                    if (hr != 0)
                    {
                        Logger.Warn(Loc.T(Str.LogEnumeratePlaybackFailed, hr.ToString("X8")));
                        return devices;
                    }

                    int count;
                    hr = collection.GetCount(out count);
                    if (hr != 0)
                    {
                        return devices;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        AudioDeviceInfo device = ReadDevice(collection, i);
                        if (device != null)
                        {
                            devices.Add(device);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(Loc.T(Str.LogReadDeviceListFailed), ex);
                }
                finally
                {
                    ReleaseComObject(collection);
                    ReleaseComObject(enumerator);
                }
            }

            DisambiguateNames(devices);
            return devices;
        }

        private static AudioDeviceInfo ReadDevice(IMMDeviceCollection collection, int index)
        {
            IMMDevice device = null;
            IPropertyStore store = null;
            try
            {
                if (collection.Item(index, out device) != 0 || device == null)
                {
                    return null;
                }

                string id;
                if (device.GetId(out id) != 0 || string.IsNullOrEmpty(id))
                {
                    return null;
                }

                string name = null;
                if (device.OpenPropertyStore(STGM.Read, out store) == 0 && store != null)
                {
                    // Copied to a local because a static readonly field cannot be
                    // passed by ref.
                    PROPERTYKEY key = PkeyDeviceFriendlyName;
                    PROPVARIANT value;
                    if (store.GetValue(ref key, out value) == 0)
                    {
                        try
                        {
                            if (value.vt == VT_LPWSTR && value.pointerValue != IntPtr.Zero)
                            {
                                name = Marshal.PtrToStringUni(value.pointerValue);
                            }
                        }
                        finally
                        {
                            // The string was allocated by the property store.
                            Ole32.PropVariantClear(ref value);
                        }
                    }
                }

                return new AudioDeviceInfo
                {
                    Id = id,
                    FriendlyName = string.IsNullOrEmpty(name) ? Loc.T(Str.DeviceUnnamed) : name,
                    DisplayName = string.IsNullOrEmpty(name) ? Loc.T(Str.DeviceUnnamed) : name
                };
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                ReleaseComObject(store);
                ReleaseComObject(device);
            }
        }

        /// <summary>
        /// Appends a short endpoint tag when two devices share a friendly name,
        /// so the dropdown never shows two identical, unselectable entries.
        /// </summary>
        private static void DisambiguateNames(List<AudioDeviceInfo> devices)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (AudioDeviceInfo device in devices)
            {
                int seen;
                counts.TryGetValue(device.FriendlyName, out seen);
                counts[device.FriendlyName] = seen + 1;
            }

            foreach (AudioDeviceInfo device in devices)
            {
                if (counts[device.FriendlyName] > 1)
                {
                    device.DisplayName = device.FriendlyName + "  [" + ShortId(device.Id) + "]";
                }
            }
        }

        private static string ShortId(string endpointId)
        {
            if (string.IsNullOrEmpty(endpointId))
            {
                return "?";
            }

            int open = endpointId.LastIndexOf('{');
            int close = endpointId.LastIndexOf('}');
            if (open >= 0 && close > open + 1)
            {
                string guid = endpointId.Substring(open + 1, close - open - 1);
                return guid.Length >= 8 ? guid.Substring(0, 8) : guid;
            }
            return endpointId;
        }

        /// <summary>ID of the current default render endpoint, or null if unavailable.</summary>
        public static string GetDefaultDeviceId(ERole role)
        {
            using (ComApartment.Enter())
            {
                IMMDeviceEnumerator enumerator = null;
                IMMDevice endpoint = null;
                try
                {
                    enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                    if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, role, out endpoint) != 0
                        || endpoint == null)
                    {
                        return null;
                    }

                    string id;
                    return endpoint.GetId(out id) == 0 ? id : null;
                }
                catch (Exception)
                {
                    return null;
                }
                finally
                {
                    ReleaseComObject(endpoint);
                    ReleaseComObject(enumerator);
                }
            }
        }

        /// <summary>
        /// True when the endpoint still exists, regardless of its state. Used to
        /// warn about a saved target that has since been unplugged.
        /// </summary>
        public static bool DeviceExists(string endpointId)
        {
            if (string.IsNullOrEmpty(endpointId))
            {
                return false;
            }

            using (ComApartment.Enter())
            {
                IMMDeviceEnumerator enumerator = null;
                IMMDevice device = null;
                try
                {
                    enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                    return enumerator.GetDevice(endpointId, out device) == 0 && device != null;
                }
                catch (Exception)
                {
                    return false;
                }
                finally
                {
                    ReleaseComObject(device);
                    ReleaseComObject(enumerator);
                }
            }
        }

        private static void ReleaseComObject(object instance)
        {
            if (instance == null)
            {
                return;
            }
            try
            {
                if (Marshal.IsComObject(instance))
                {
                    Marshal.ReleaseComObject(instance);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}

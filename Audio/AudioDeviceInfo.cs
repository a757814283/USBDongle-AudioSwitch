using System;

namespace USBDongle_AudioSwitch.Audio
{
    /// <summary>A single render endpoint as shown in the configuration dropdowns.</summary>
    internal sealed class AudioDeviceInfo
    {
        /// <summary>Endpoint ID string, e.g. "{0.0.0.00000000}.{52e88adb-...}".</summary>
        public string Id { get; set; }

        /// <summary>Friendly name from the property store, e.g. "Headphones (AWPRO H Wireless Game)".</summary>
        public string FriendlyName { get; set; }

        /// <summary>What the combo box displays. May carry a suffix when names collide.</summary>
        public string DisplayName { get; set; }

        public bool IsDefault { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}

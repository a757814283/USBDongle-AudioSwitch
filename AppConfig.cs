using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace USBDongle_AudioSwitch
{
    /// <summary>
    /// Persisted settings, stored as JSON under %APPDATA%\USBDongle_AudioSwitch.
    ///
    /// The defaults reproduce the behaviour of the original audio_switch.ps1, so
    /// a fresh install with no config file behaves exactly like the script did.
    /// </summary>
    [DataContract(Name = "config", Namespace = "")]
    public sealed class AppConfig
    {
        public const string DefaultHidMatch = "VID_413C&PID_A529&MI_05&COL02";
        public const string DefaultSignatureOnHex = "08 c0 09 03 00 01 cc 0f";
        public const string DefaultSignatureOffHex = "08 c0 09 03 00 01 dd 1e";
        public const int DefaultSignatureLength = 8;
        public const int DefaultCooldownMs = 1500;
        public const int DefaultReopenDelayMs = 2000;
        public const int DefaultReportBufferSize = 256;

        // ---- audio targets -------------------------------------------------

        /// <summary>Endpoint ID to select when the headset powers on.</summary>
        [DataMember(Name = "headsetDeviceId")]
        public string HeadsetDeviceId { get; set; }

        /// <summary>Endpoint ID to select when the headset powers off.</summary>
        [DataMember(Name = "speakerDeviceId")]
        public string SpeakerDeviceId { get; set; }

        /// <summary>
        /// Original script set all three roles. Turning this off writes only the
        /// Multimedia role, which is what a plain "Set as Default Device" does.
        /// </summary>
        [IgnoreDataMember]
        public bool SetAllRoles
        {
            get { return SetAllRolesRaw ?? true; }
            set { SetAllRolesRaw = value; }
        }

        /// <summary>
        /// Nullable so an absent key can be told apart from an explicit false.
        /// The serializer builds instances without running the constructor, so
        /// without this an omitted switch would silently read as false and
        /// defeat the documented default.
        /// </summary>
        [DataMember(Name = "setAllRoles")]
        private bool? SetAllRolesRaw { get; set; }

        /// <summary>Optional SoundVolumeView.exe, used only if the native COM call fails.</summary>
        [DataMember(Name = "soundVolumeViewPath")]
        public string SoundVolumeViewPath { get; set; }

        /// <summary>
        /// UI language as "auto", "en" or "zh". Stored as a string rather than an
        /// enum so a hand-edited file stays readable and an unknown value
        /// degrades to "auto" instead of failing to deserialize.
        /// </summary>
        [DataMember(Name = "language")]
        public string Language { get; set; }

        /// <summary>
        /// <see cref="Language"/> as an enum, for callers that switch on it. The
        /// stored form stays a string so the file is readable and an unknown
        /// value degrades to Auto instead of failing to deserialize.
        /// </summary>
        [IgnoreDataMember]
        public AppLanguage LanguageValue
        {
            get { return Loc.Parse(Language); }
            set { Language = Loc.ToConfigValue(value); }
        }

        // ---- HID detection -------------------------------------------------

        /// <summary>
        /// Substring of the device instance ID identifying the receiver's HID
        /// collection. Matched case-insensitively; the port suffix is intentionally
        /// absent because it changes with the USB port.
        /// </summary>
        [DataMember(Name = "hidMatch")]
        public string HidMatch { get; set; }

        [DataMember(Name = "signatureOnHex")]
        public string SignatureOnHex { get; set; }

        [DataMember(Name = "signatureOffHex")]
        public string SignatureOffHex { get; set; }

        /// <summary>How many leading bytes of a report participate in matching.</summary>
        [DataMember(Name = "signatureLength")]
        public int SignatureLength { get; set; }

        [DataMember(Name = "cooldownMs")]
        public int CooldownMs { get; set; }

        [DataMember(Name = "reopenDelayMs")]
        public int ReopenDelayMs { get; set; }

        /// <summary>
        /// When set, the last byte of a signature must equal the XOR of the
        /// preceding ones before a report counts as a match. Both shipped
        /// signatures satisfy this, so it only rejects corrupted reads.
        /// </summary>
        [DataMember(Name = "verifyChecksum")]
        public bool VerifyChecksum { get; set; }

        [DataMember(Name = "logUnknownReports")]
        public bool LogUnknownReports { get; set; }

        // ---- startup -------------------------------------------------------

        [IgnoreDataMember]
        public bool StartMonitoringOnLaunch
        {
            get { return StartMonitoringOnLaunchRaw ?? true; }
            set { StartMonitoringOnLaunchRaw = value; }
        }

        /// <summary>Nullable for the same reason as <see cref="SetAllRolesRaw"/>.</summary>
        [DataMember(Name = "startMonitoringOnLaunch")]
        private bool? StartMonitoringOnLaunchRaw { get; set; }

        // ---- non-persisted helpers ----------------------------------------

        [IgnoreDataMember]
        public byte[] SignatureOnBytes
        {
            get { return Hex.ParseOrEmpty(SignatureOnHex); }
        }

        [IgnoreDataMember]
        public byte[] SignatureOffBytes
        {
            get { return Hex.ParseOrEmpty(SignatureOffHex); }
        }

        public AppConfig()
        {
            // Mirror the original script: all three roles, and start monitoring
            // as soon as the program launches.
            SetAllRoles = true;
            StartMonitoringOnLaunch = true;
            LogUnknownReports = false;
            Validate();
        }

        /// <summary>
        /// Fills in anything absent or out of range.
        ///
        /// Deliberately does not touch the boolean switches: the data contract
        /// serializer builds instances without invoking the constructor (and
        /// skips property initializers), so forcing "true" here would silently
        /// override a user who set the option to false.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(HidMatch))
            {
                HidMatch = DefaultHidMatch;
            }

            byte[] onBytes;
            if (!Hex.TryParse(SignatureOnHex, out onBytes) || onBytes.Length == 0)
            {
                SignatureOnHex = DefaultSignatureOnHex;
            }

            byte[] offBytes;
            if (!Hex.TryParse(SignatureOffHex, out offBytes) || offBytes.Length == 0)
            {
                SignatureOffHex = DefaultSignatureOffHex;
            }

            int longest = Math.Max(SignatureOnBytes.Length, SignatureOffBytes.Length);
            if (SignatureLength <= 0 || SignatureLength > longest)
            {
                SignatureLength = Math.Min(DefaultSignatureLength, Math.Max(1, longest));
            }

            CooldownMs = Clamp(CooldownMs, 0, 60000, DefaultCooldownMs);
            ReopenDelayMs = Clamp(ReopenDelayMs, 100, 60000, DefaultReopenDelayMs);

            if (HeadsetDeviceId == null)
            {
                HeadsetDeviceId = string.Empty;
            }
            if (SpeakerDeviceId == null)
            {
                SpeakerDeviceId = string.Empty;
            }
            if (SoundVolumeViewPath == null)
            {
                SoundVolumeViewPath = string.Empty;
            }
            if (string.IsNullOrWhiteSpace(Language))
            {
                Language = Loc.ToConfigValue(AppLanguage.Auto);
            }
        }

        private static int Clamp(int value, int min, int max, int fallback)
        {
            if (value == 0)
            {
                return fallback;
            }
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Validate();
        }

        // ---- persistence ---------------------------------------------------

        public static AppConfig LoadOrDefault(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Logger.Info(Loc.T(Str.LogConfigNotFound, path));
                    return new AppConfig();
                }

                // Read as text and re-encode, rather than handing the FileStream
                // straight to the serializer. File.ReadAllText detects and strips
                // a byte-order mark; the JSON reader rejects one outright
                // ("unexpected character"), and Notepad writes a BOM by default -
                // so a hand-edited config would otherwise be treated as corrupt
                // and every setting in it silently replaced by the defaults.
                string text = File.ReadAllText(path);

                using (MemoryStream stream = new MemoryStream(new UTF8Encoding(false).GetBytes(text)))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppConfig));
                    AppConfig loaded = serializer.ReadObject(stream) as AppConfig;
                    if (loaded == null)
                    {
                        Logger.Warn(Loc.T(Str.LogConfigEmpty));
                        return new AppConfig();
                    }
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(Loc.T(Str.LogConfigParseFailed), ex);
                BackupCorrupt(path);
                return new AppConfig();
            }
        }

        /// <summary>Returns null on success, or an error message.</summary>
        public string Save(string path)
        {
            try
            {
                AppPaths.EnsureConfigDirectory();

                // Write to a temporary file and swap it in, so a crash midway
                // through cannot leave a half-written config behind.
                string temp = path + ".tmp";

                using (FileStream stream = File.Create(temp))
                using (XmlWriter writer = JsonReaderWriterFactory.CreateJsonWriter(
                           stream, new UTF8Encoding(false), false, true, "  "))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppConfig));
                    serializer.WriteObject(writer, this);
                    writer.Flush();
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(temp, path);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static void BackupCorrupt(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }
                string backup = path + ".bad";
                if (File.Exists(backup))
                {
                    File.Delete(backup);
                }
                File.Move(path, backup);
            }
            catch (Exception)
            {
            }
        }
    }
}

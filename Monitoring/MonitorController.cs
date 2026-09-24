using System;
using USBDongle_AudioSwitch.Audio;
using USBDongle_AudioSwitch.Interop;

namespace USBDongle_AudioSwitch.Monitoring
{
    /// <summary>
    /// Ties the HID monitor to the audio switcher: a power-on report selects the
    /// headset endpoint, a power-off report selects the speaker endpoint.
    ///
    /// Signal handling runs directly on the monitor thread. The UI subscribes
    /// from the form thread and is responsible for marshalling its own updates.
    /// </summary>
    internal sealed class MonitorController : IDisposable
    {
        private readonly HidMonitor _monitor = new HidMonitor();
        private AppConfig _config;
        private string _devicePath;

        /// <summary>
        /// Last status published, kept as a key rather than as text so a language
        /// change can re-render it instead of leaving a stale string on screen.
        /// </summary>
        private Str? _statusKey;

        public event Action<bool> RunningChanged;

        /// <summary>Short status-bar text; may fire on the monitor thread.</summary>
        public event Action<string> StatusChanged;

        public event Action<string> DevicePathChanged;

        /// <summary>
        /// Raised for every matched report, whether or not it resulted in a
        /// switch. The UI uses it purely for its event counter.
        /// </summary>
        public event Action<HidSignal> SignalDetected;

        public MonitorController(AppConfig config)
        {
            _config = config;

            _monitor.Message += OnMonitorMessage;
            _monitor.DeviceOpened += OnDeviceOpened;
            _monitor.SignalDetected += OnSignalDetected;
        }

        public bool IsRunning
        {
            get { return _monitor.IsRunning; }
        }

        public string DevicePath
        {
            get { return _devicePath; }
        }

        public AppConfig Config
        {
            get { return _config; }
        }

        /// <summary>
        /// Applies new settings. Changes take effect on the next restart of the
        /// monitor, which the caller triggers by stopping and starting.
        /// </summary>
        public void UpdateConfig(AppConfig config)
        {
            _config = config;
        }

        public void Start()
        {
            if (_monitor.IsRunning)
            {
                return;
            }

            _config.Validate();
            Logger.Info(Loc.T(Str.LogStartMonitoring, _config.HidMatch,
                _config.SignatureLength, _config.CooldownMs));
            Logger.Info(Loc.T(Str.LogStartTargetOn, DescribeTarget(_config.HeadsetDeviceId)));
            Logger.Info(Loc.T(Str.LogStartTargetOff, DescribeTarget(_config.SpeakerDeviceId)));

            _monitor.Start(_config);
            RaiseRunningChanged(true);
            SetStatus(Str.StatusMonitoring);
        }

        public void Stop()
        {
            if (!_monitor.IsRunning)
            {
                return;
            }

            _monitor.Stop();
            _devicePath = null;
            RaiseDevicePathChanged(null);
            Logger.Info(Loc.T(Str.LogStopMonitoring));
            RaiseRunningChanged(false);
            SetStatus(Str.StatusStopped);
        }

        /// <summary>
        /// Re-publishes the current status after the language changed, so the
        /// status bar does not keep showing the previous language's wording.
        /// </summary>
        public void RefreshStatusText()
        {
            if (_statusKey.HasValue)
            {
                SetStatus(_statusKey.Value);
            }
        }

        public void Dispose()
        {
            _monitor.Message -= OnMonitorMessage;
            _monitor.DeviceOpened -= OnDeviceOpened;
            _monitor.SignalDetected -= OnSignalDetected;
            _monitor.Dispose();
        }

        // ---- monitor events -------------------------------------------------

        /// <summary>
        /// Monitor text goes straight to the log file. The form renders whatever
        /// the logger emits, so there is no separate notification event to keep
        /// in sync - and no risk of the same line being written twice.
        /// </summary>
        private void OnMonitorMessage(string message)
        {
            Logger.Write(message);
        }

        private void OnDeviceOpened(string path)
        {
            _devicePath = path;
            RaiseDevicePathChanged(path);
        }

        private void OnSignalDetected(HidSignal signal, string reportHex)
        {
            Action<HidSignal> detected = SignalDetected;
            if (detected != null)
            {
                detected(signal);
            }

            bool powerOn = signal == HidSignal.PowerOn;
            string targetId = powerOn ? _config.HeadsetDeviceId : _config.SpeakerDeviceId;
            string label = Loc.T(powerOn ? Str.LogHeadsetPowerOn : Str.LogHeadsetPowerOff);

            if (string.IsNullOrWhiteSpace(targetId))
            {
                Logger.Warn(Loc.T(Str.LogTargetNotConfigured, label));
                SetStatus(Str.StatusNoTargets);
                return;
            }

            // Compare against the live default rather than a cached value: the
            // user may have changed the default device manually since the last
            // switch, and a stale cache would suppress a switch that is needed.
            string current = AudioDeviceService.GetDefaultDeviceId(ERole.eMultimedia);
            if (string.Equals(current, targetId, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info(Loc.T(Str.LogAlreadyTarget, label));
                SetStatus(Str.StatusAlreadyTarget);
                return;
            }

            Logger.Info(Loc.T(Str.LogSwitchingTo, DescribeTarget(targetId)));

            string error;
            if (AudioSwitcher.TrySetDefault(targetId, _config.SetAllRoles,
                    _config.SoundVolumeViewPath, out error))
            {
                Logger.Info(Loc.T(Str.LogSwitchSucceeded, reportHex));
                SetStatus(powerOn ? Str.StatusSwitchedToHeadset : Str.StatusSwitchedToSpeaker);
            }
            else
            {
                Logger.Error(Loc.T(Str.LogSwitchFailed, error));
                SetStatus(Str.StatusSwitchFailed);
            }
        }

        private static string DescribeTarget(string endpointId)
        {
            if (string.IsNullOrWhiteSpace(endpointId))
            {
                return Loc.T(Str.LogUnresolvedTarget);
            }

            // Prefer the friendly name; the raw GUID is a poor log line.
            foreach (AudioDeviceInfo device in AudioDeviceService.GetPlaybackDevices())
            {
                if (string.Equals(device.Id, endpointId, StringComparison.OrdinalIgnoreCase))
                {
                    return device.FriendlyName + "  " + endpointId;
                }
            }

            return Loc.T(Str.LogDeviceUnavailable, endpointId);
        }

        private void RaiseRunningChanged(bool running)
        {
            Action<bool> handler = RunningChanged;
            if (handler != null)
            {
                handler(running);
            }
        }

        private void RaiseDevicePathChanged(string path)
        {
            Action<string> handler = DevicePathChanged;
            if (handler != null)
            {
                handler(path);
            }
        }

        private void SetStatus(Str key)
        {
            _statusKey = key;

            Action<string> handler = StatusChanged;
            if (handler != null)
            {
                handler(Loc.T(key));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using USBDongle_AudioSwitch.Audio;
using USBDongle_AudioSwitch.Interop;
using USBDongle_AudioSwitch.Monitoring;

namespace USBDongle_AudioSwitch
{
    public partial class MainForm : Form
    {
        /// <summary>Log lines kept in the text box before the oldest are dropped.</summary>
        private const int MaxLogLines = 2000;

        /// <summary>
        /// Placeholder shown when no target device has been chosen yet.
        ///
        /// Built on demand rather than cached in a field: the caption follows the
        /// UI language, and the device lists are rebuilt whenever it changes.
        /// </summary>
        private static AudioDeviceInfo NoSelection()
        {
            string caption = Loc.T(Str.DeviceNoneSelected);
            return new AudioDeviceInfo
            {
                Id = string.Empty,
                FriendlyName = caption,
                DisplayName = caption
            };
        }

        private readonly bool _startHidden;
        private readonly MonitorController _controller;
        private readonly LogWheelRouter _logWheelRouter;
        private AppConfig _config;

        /// <summary>Suppresses change handlers while the UI is being populated.</summary>
        private bool _loading;

        private bool _allowVisible;
        private bool _startupDone;
        private bool _exiting;
        private bool _trayHintShown;
        private int _logLineCount;
        private int _eventCount;

        public MainForm() : this(false)
        {
        }

        public MainForm(bool startHidden)
        {
            _startHidden = startHidden;
            _allowVisible = !startHidden;

            InitializeComponent();

            _config = AppConfig.LoadOrDefault(AppPaths.ConfigFile);

            // The config names the language, so the window can only be labelled
            // once it has been read. Anything logged before this point - the
            // config loader's own messages - is in the system language.
            Loc.Apply(Loc.Parse(_config.Language));

            ApplyLocalization();
            LoadConfigIntoUi();
            ReloadDeviceLists();

            _controller = new MonitorController(_config);
            _controller.StatusChanged += OnControllerStatus;
            _controller.DevicePathChanged += OnControllerDevicePath;
            _controller.RunningChanged += OnControllerRunningChanged;
            _controller.SignalDetected += OnControllerSignal;

            Logger.LineWritten += OnLogLine;

            // Lets the wheel scroll the log on hover; see LogWheelRouter.
            _logWheelRouter = new LogWheelRouter(_txtLog);
            Application.AddMessageFilter(_logWheelRouter);

            WireEvents();
        }

        private void WireEvents()
        {
            _btnRefreshDevices.Click += (sender, e) => ReloadDeviceLists();
            _btnListHid.Click += (sender, e) => ShowHidDeviceDialog();
            _btnSave.Click += (sender, e) => SaveConfigFromUi(true);
            _btnStart.Click += (sender, e) => StartMonitoring();
            _btnStop.Click += (sender, e) => _controller.Stop();
            _btnClearLog.Click += (sender, e) => ClearLog();
            _btnOpenFolder.Click += (sender, e) => OpenConfigFolder();

            _txtSigOn.TextChanged += (sender, e) => UpdateSignatureIndicators();
            _txtSigOff.TextChanged += (sender, e) => UpdateSignatureIndicators();

            _chkAutoStart.CheckedChanged += OnAutoStartChanged;

            _cboLanguage.SelectedIndexChanged += OnLanguageChanged;

            _trayIcon.DoubleClick += (sender, e) => ShowFromTray();
            _menuShow.Click += (sender, e) => ShowFromTray();
            _menuStart.Click += (sender, e) => StartMonitoring();
            _menuStop.Click += (sender, e) => _controller.Stop();
            _menuExit.Click += (sender, e) => ExitApplication();
        }

        // ---- localization ---------------------------------------------------

        /// <summary>
        /// (Re)labels every visible string.
        ///
        /// Runs once at start-up, with <see cref="Loc.Current"/> already settled,
        /// and again whenever the language changes. That is why no caption may
        /// live in the designer: switching languages has to relabel the window in
        /// place rather than rebuild it, which would lose the running monitor.
        /// </summary>
        private void ApplyLocalization()
        {
            Text = Loc.T(Str.WindowTitle);
            _trayIcon.Text = Truncate(Loc.T(Str.TrayTooltip), 63);

            _audioGroup.Text = Loc.T(Str.GroupAudio);
            _lblHeadset.Text = Loc.T(Str.LabelHeadsetTarget);
            _lblSpeaker.Text = Loc.T(Str.LabelSpeakerTarget);
            _btnRefreshDevices.Text = Loc.T(Str.ButtonRefreshDevices);

            _hidGroup.Text = Loc.T(Str.GroupHid);
            _lblHidMatch.Text = Loc.T(Str.LabelHidMatch);
            _btnListHid.Text = Loc.T(Str.ButtonListHid);
            _lblSigOn.Text = Loc.T(Str.LabelSignatureOn);
            _lblSigOff.Text = Loc.T(Str.LabelSignatureOff);
            _lblSigLength.Text = Loc.T(Str.LabelSignatureLength);
            _lblCooldown.Text = Loc.T(Str.LabelCooldown);
            _lblReopen.Text = Loc.T(Str.LabelReopenDelay);
            _chkVerifyChecksum.Text = Loc.T(Str.CheckVerifyChecksum);
            _chkLogUnknown.Text = Loc.T(Str.CheckLogUnknownReports);

            _chkAutoStart.Text = Loc.T(Str.CheckAutoStart);
            _btnSave.Text = Loc.T(Str.ButtonSave);
            _btnStart.Text = Loc.T(Str.ButtonStart);
            _btnStop.Text = Loc.T(Str.ButtonStop);
            _btnClearLog.Text = Loc.T(Str.ButtonClearLog);
            _btnOpenFolder.Text = Loc.T(Str.ButtonOpenFolder);

            _logGroup.Text = Loc.T(Str.GroupLog);

            _menuShow.Text = Loc.T(Str.MenuShow);
            _menuStart.Text = Loc.T(Str.MenuStart);
            _menuStop.Text = Loc.T(Str.MenuStop);
            _menuExit.Text = Loc.T(Str.MenuExit);

            _lblLanguage.Text = Loc.T(Str.LabelLanguage);
            PopulateLanguageChoices();

            // The controller owns the status text; before it exists the window
            // shows the idle caption itself. Either way it must be re-rendered,
            // or the status bar keeps the previous language's wording.
            if (_controller != null)
            {
                _controller.RefreshStatusText();
                SetDevicePathLabel(_controller.DevicePath);
            }
            else
            {
                _lblStatus.Text = Loc.T(Str.StatusReady);
                SetDevicePathLabel(null);
            }

            _lblEventCount.Text = Loc.T(Str.StatusEventPrefix) + _eventCount;
            UpdateSignatureIndicators();
        }

        /// <summary>
        /// Fills the language picker, keeping the current selection.
        ///
        /// Rebuilt rather than merely re-labelled because a ComboBox caches each
        /// item's rendering: dropping the items back in is what makes the entries
        /// follow the language that was just chosen.
        /// </summary>
        private void PopulateLanguageChoices()
        {
            bool wasLoading = _loading;
            _loading = true;
            try
            {
                int selected = _cboLanguage.SelectedIndex;

                _cboLanguage.Items.Clear();
                _cboLanguage.Items.Add(new LanguageChoice(AppLanguage.Auto));
                _cboLanguage.Items.Add(new LanguageChoice(AppLanguage.English));
                _cboLanguage.Items.Add(new LanguageChoice(AppLanguage.Chinese));

                _cboLanguage.SelectedIndex = selected < 0 ? 0 : selected;
            }
            finally
            {
                _loading = wasLoading;
            }
        }

        /// <summary>
        /// One entry in the language picker. Each language names itself, so the
        /// entry stays findable whichever language is currently on screen.
        /// </summary>
        private sealed class LanguageChoice
        {
            public LanguageChoice(AppLanguage language)
            {
                Language = language;
            }

            public AppLanguage Language { get; private set; }

            public override string ToString()
            {
                switch (Language)
                {
                    case AppLanguage.English: return Loc.T(Str.LanguageEnglish);
                    case AppLanguage.Chinese: return Loc.T(Str.LanguageChinese);
                    default: return Loc.T(Str.LanguageAuto);
                }
            }
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (_loading)
            {
                return;
            }

            LanguageChoice choice = _cboLanguage.SelectedItem as LanguageChoice;
            if (choice == null || choice.Language == _config.LanguageValue)
            {
                return;
            }

            _config.LanguageValue = choice.Language;
            Loc.Apply(choice.Language);

            ApplyLocalization();

            // Device rows carry localized captions of their own - "(none)",
            // "(unnamed device)", "(saved, currently unavailable)" - so they are
            // rebuilt rather than relabelled.
            ReloadDeviceLists();

            string error = _config.Save(AppPaths.ConfigFile);
            if (error != null)
            {
                MessageBox.Show(this, Loc.T(Str.MessageSaveFailed, error),
                    Loc.T(Str.MessageErrorCaption),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Runs the start-up sequence once the window handle exists.
        ///
        /// Deliberately not hooked to Shown/Load: on a --tray launch the window is
        /// never made visible, and WinForms raises those events only from
        /// SetVisibleCore(true) - so monitoring would silently never start.
        /// Handle creation always happens, visible or not.
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (_startupDone)
            {
                return;
            }
            _startupDone = true;

            RunStartupSequence();
        }

        private void RunStartupSequence()
        {
            Logger.Info(Loc.T(Str.LogConfigFile) + AppPaths.ConfigFile);
            Logger.Info(Loc.T(Str.LogLogFile) + Logger.LogPath);

            if (_config.HeadsetDeviceId.Length == 0 || _config.SpeakerDeviceId.Length == 0)
            {
                Logger.Warn(Loc.T(Str.LogNoTargetsConfigured));
            }

            if (AutoStartManager.IsStale())
            {
                Logger.Warn(Loc.T(Str.LogAutoStartStale));
            }

            if (_config.StartMonitoringOnLaunch)
            {
                StartMonitoring();
            }
        }

        // ---- window lifetime -----------------------------------------------

        /// <summary>
        /// Keeps the window from ever being shown on a --tray launch, while still
        /// forcing handle creation so the tray icon and marshalling work.
        /// </summary>
        protected override void SetVisibleCore(bool value)
        {
            if (!_allowVisible)
            {
                if (!IsHandleCreated)
                {
                    CreateHandle();
                }
                value = false;
            }

            base.SetVisibleCore(value);
        }

        /// <summary>
        /// Follows the taskbar as it moves between the light and the dark theme.
        ///
        /// The notification area is composited straight over the taskbar and the
        /// tray icon has no plate behind it, so its ink has to be the colour the
        /// taskbar is not - see AppIcons. The shell announces the change as a
        /// WM_SETTINGCHANGE naming "ImmersiveColorSet". Nothing else here has to
        /// react: the window follows the system colours and font on its own.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == Win32.WM_SETTINGCHANGE && MayBeThemeChange(m.LParam))
            {
                RefreshTrayIcon();
            }
        }

        /// <summary>
        /// True for the broadcasts that can carry a theme change.
        ///
        /// A null lParam is the older "some system setting changed" form that
        /// installers and control-panel applets still send. Re-reading the theme
        /// for one costs a registry read and changes nothing when the taskbar has
        /// not moved, which is cheaper than ignoring a real change.
        /// </summary>
        private static bool MayBeThemeChange(IntPtr lParam)
        {
            string area = Win32.SettingChangeArea(lParam);
            return area == null ||
                   string.Equals(area, "ImmersiveColorSet", StringComparison.Ordinal);
        }

        /// <summary>
        /// Swaps in the other tray artwork once the taskbar has changed colour.
        ///
        /// <see cref="AppIcons.Tray"/> answers with the icon the current theme
        /// wants, so comparing it against the one already on screen is what tells
        /// whether anything actually moved - assigning unconditionally would poke
        /// the shell with an identical icon on every unrelated settings broadcast.
        /// A swap logs nothing of its own: loading the other artwork is what
        /// writes the theme to the log.
        /// </summary>
        private void RefreshTrayIcon()
        {
            if (_trayIcon == null)
            {
                return;
            }

            Icon wanted = AppIcons.Tray;
            if (ReferenceEquals(_trayIcon.Icon, wanted))
            {
                return;
            }

            _trayIcon.Icon = wanted;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Closing the window means "go to the tray"; only the tray menu's
            // Exit (or a shutdown) really terminates the program.
            if (!_exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Logger.LineWritten -= OnLogLine;

            if (_logWheelRouter != null)
            {
                Application.RemoveMessageFilter(_logWheelRouter);
            }

            if (_controller != null)
            {
                _controller.Dispose();
            }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
            }

            base.OnFormClosed(e);
        }

        private void HideToTray()
        {
            Hide();

            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _trayIcon.ShowBalloonTip(3000, Loc.T(Str.MessageStillRunningTitle),
                    Loc.T(Str.MessageStillRunningBody), ToolTipIcon.Info);
            }

            Logger.Info(Loc.T(Str.LogMinimisedToTray));
        }

        /// <summary>Restores and focuses the window. Safe to call from any thread.</summary>
        public void ShowFromTray()
        {
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(ShowFromTray));
                }
                catch (Exception)
                {
                }
                return;
            }

            _allowVisible = true;
            ShowInTaskbar = true;

            if (!Visible)
            {
                Show();
            }
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            Activate();
            BringToFront();

            try
            {
                Win32.SetForegroundWindow(Handle);
            }
            catch (Exception)
            {
            }
        }

        private void ExitApplication()
        {
            _exiting = true;
            Close();
        }

        // ---- monitoring ------------------------------------------------------

        private void StartMonitoring()
        {
            if (!SaveConfigFromUi(true))
            {
                return;
            }

            _controller.UpdateConfig(_config);

            if (_config.HeadsetDeviceId.Length == 0 || _config.SpeakerDeviceId.Length == 0)
            {
                Logger.Warn(Loc.T(Str.LogTargetsNotChosen));
            }

            // Settings are snapshotted when the monitor thread starts, so an
            // already-running monitor has to be cycled to pick up edits.
            if (_controller.IsRunning)
            {
                _controller.Stop();
            }

            _controller.Start();
        }

        private void OnControllerStatus(string status)
        {
            RunOnUi(() =>
            {
                _lblStatus.Text = status;
                _trayIcon.Text = Truncate(Loc.T(Str.TrayTooltip) + " - " + status, 63);
            });
        }

        private void OnControllerDevicePath(string path)
        {
            RunOnUi(() => SetDevicePathLabel(path));
        }

        /// <summary>
        /// Writes the device path straight to the label, without marshalling.
        ///
        /// Separate from <see cref="OnControllerDevicePath"/> because the language
        /// switch has to relabel it from the constructor and from the UI thread,
        /// where <see cref="RunOnUi"/> would drop the update - the handle does not
        /// exist yet at that point, so the first caption would never appear.
        /// </summary>
        private void SetDevicePathLabel(string path)
        {
            _lblDevicePath.Text = string.IsNullOrEmpty(path)
                ? Loc.T(Str.StatusDeviceNotConnected)
                : Loc.T(Str.StatusDevicePrefix) + path;
        }

        private void OnControllerRunningChanged(bool running)
        {
            RunOnUi(() =>
            {
                _btnStart.Enabled = !running;
                _btnStop.Enabled = running;
                _menuStart.Enabled = !running;
                _menuStop.Enabled = running;
            });
        }

        private void OnControllerSignal(HidSignal signal)
        {
            RunOnUi(() =>
            {
                _eventCount++;
                _lblEventCount.Text = Loc.T(Str.StatusEventPrefix) + _eventCount;
            });
        }

        // ---- logging ---------------------------------------------------------

        private void OnLogLine(string line)
        {
            RunOnUi(() =>
            {
                if (_txtLog.IsDisposed || !_txtLog.IsHandleCreated)
                {
                    return;
                }

                // Both sampled before appending: afterwards the box has grown
                // and the answers describe the new state, not the reader's.
                int scrollBefore = Win32.GetScrollPos(_txtLog.Handle, Win32.SB_VERT);
                bool wasAtBottom = IsLogAtBottom();

                // AppendText parks the caret at the end of the text, and the
                // control scrolls to keep the caret visible - so the append
                // itself drags the view down even with no explicit
                // ScrollToCaret. Save and restore around it.
                _txtLog.AppendText(line + Environment.NewLine);
                _logLineCount++;

                if (_logLineCount > MaxLogLines)
                {
                    TrimLog();
                }

                if (wasAtBottom)
                {
                    ScrollLogToEnd();
                    return;
                }

                // The reader is somewhere in the history; put the view back.
                int drift = Win32.GetScrollPos(_txtLog.Handle, Win32.SB_VERT) - scrollBefore;
                if (drift != 0)
                {
                    Win32.SendMessageW(_txtLog.Handle, Win32.EM_LINESCROLL,
                        IntPtr.Zero, new IntPtr(-drift));
                }
            });
        }

        /// <summary>
        /// True when the newest line is already in view, read straight off the
        /// scrollbar rather than inferred from the caret.
        /// </summary>
        private bool IsLogAtBottom()
        {
            int min;
            int max;
            if (!Win32.GetScrollRange(_txtLog.Handle, Win32.SB_VERT, out min, out max))
            {
                return true;
            }

            // When the text fits, the range is empty and there is no scrollbar
            // to speak of - treating that as "at the bottom" is harmless.
            return max <= min ||
                   Win32.GetScrollPos(_txtLog.Handle, Win32.SB_VERT) >= max;
        }

        private void ScrollLogToEnd()
        {
            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.SelectionLength = 0;
            _txtLog.ScrollToCaret();
        }

        private void TrimLog()
        {
            string[] lines = _txtLog.Lines;
            int keep = MaxLogLines / 2;
            if (lines.Length <= keep)
            {
                return;
            }

            string[] tail = new string[keep];
            Array.Copy(lines, lines.Length - keep, tail, 0, keep);
            _txtLog.Lines = tail;
            _logLineCount = keep;
        }

        private void ClearLog()
        {
            Logger.Clear();
            _txtLog.Clear();
            _logLineCount = 0;
            Logger.Info(Loc.T(Str.LogLogCleared));
        }

        private void OpenConfigFolder()
        {
            try
            {
                AppPaths.EnsureConfigDirectory();
                Process.Start("explorer.exe", "\"" + AppPaths.ConfigDirectory + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Loc.T(Str.MessageOpenFolderFailed, ex.Message),
                    Loc.T(Str.MessageErrorCaption),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Marshals a UI update; a no-op once the form is gone.</summary>
        private void RunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(action);
                }
                catch (Exception)
                {
                    // Racing with form disposal - nothing left to update.
                }
                return;
            }

            action();
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }
            return text.Substring(0, maxLength - 1) + "…";
        }

        // ---- configuration ---------------------------------------------------

        private void LoadConfigIntoUi()
        {
            _loading = true;
            try
            {
                _txtHidMatch.Text = _config.HidMatch;
                _txtSigOn.Text = _config.SignatureOnHex;
                _txtSigOff.Text = _config.SignatureOffHex;

                _numSigLength.Value = Clamp(_config.SignatureLength,
                    _numSigLength.Minimum, _numSigLength.Maximum);
                _numCooldown.Value = Clamp(_config.CooldownMs,
                    _numCooldown.Minimum, _numCooldown.Maximum);
                _numReopen.Value = Clamp(_config.ReopenDelayMs,
                    _numReopen.Minimum, _numReopen.Maximum);

                _chkVerifyChecksum.Checked = _config.VerifyChecksum;
                _chkLogUnknown.Checked = _config.LogUnknownReports;
                _chkAutoStart.Checked = AutoStartManager.IsEnabled();
            }
            finally
            {
                _loading = false;
            }

            UpdateSignatureIndicators();
        }

        private static decimal Clamp(int value, decimal min, decimal max)
        {
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

        /// <summary>Returns false when the input is invalid or could not be written.</summary>
        private bool SaveConfigFromUi(bool showErrors)
        {
            string problem = ValidateInput();
            if (problem != null)
            {
                if (showErrors)
                {
                    MessageBox.Show(this, problem, Loc.T(Str.MessageInvalidConfigCaption),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            _config.HidMatch = _txtHidMatch.Text.Trim();
            _config.SignatureOnHex = _txtSigOn.Text.Trim();
            _config.SignatureOffHex = _txtSigOff.Text.Trim();
            _config.SignatureLength = (int)_numSigLength.Value;
            _config.CooldownMs = (int)_numCooldown.Value;
            _config.ReopenDelayMs = (int)_numReopen.Value;
            _config.VerifyChecksum = _chkVerifyChecksum.Checked;
            _config.LogUnknownReports = _chkLogUnknown.Checked;

            // Keep whatever was stored when a combo has no usable selection,
            // otherwise an empty device list would silently erase the target.
            _config.HeadsetDeviceId = GetSelectedDeviceId(_cboHeadset, _config.HeadsetDeviceId);
            _config.SpeakerDeviceId = GetSelectedDeviceId(_cboSpeaker, _config.SpeakerDeviceId);

            _config.Validate();

            string error = _config.Save(AppPaths.ConfigFile);
            if (error != null)
            {
                if (showErrors)
                {
                    MessageBox.Show(this, Loc.T(Str.MessageSaveFailed, error),
                        Loc.T(Str.MessageErrorCaption),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            return true;
        }

        private string ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(_txtHidMatch.Text))
            {
                return Loc.T(Str.ErrorHidMatchEmpty);
            }

            byte[] onBytes;
            if (!Hex.TryParse(_txtSigOn.Text, out onBytes) || onBytes.Length == 0)
            {
                return Loc.T(Str.ErrorSignatureOnUnparseable);
            }

            byte[] offBytes;
            if (!Hex.TryParse(_txtSigOff.Text, out offBytes) || offBytes.Length == 0)
            {
                return Loc.T(Str.ErrorSignatureOffUnparseable);
            }

            int length = (int)_numSigLength.Value;
            int available = Math.Min(onBytes.Length, offBytes.Length);
            if (length > available)
            {
                return Loc.T(Str.ErrorSignatureTooLong, length, available);
            }

            return null;
        }

        private static string GetSelectedDeviceId(ComboBox combo, string fallback)
        {
            AudioDeviceInfo selected = combo.SelectedItem as AudioDeviceInfo;
            if (selected == null || string.IsNullOrEmpty(selected.Id))
            {
                return fallback;
            }
            return selected.Id;
        }

        private void ReloadDeviceLists()
        {
            List<AudioDeviceInfo> devices = AudioDeviceService.GetPlaybackDevices();

            FillDeviceCombo(_cboHeadset, devices, _config.HeadsetDeviceId);
            FillDeviceCombo(_cboSpeaker, devices, _config.SpeakerDeviceId);

            Logger.Info(Loc.T(Str.LogDevicesLoaded, devices.Count));
        }

        private static void FillDeviceCombo(ComboBox combo, List<AudioDeviceInfo> devices, string savedId)
        {
            combo.BeginUpdate();
            try
            {
                combo.Items.Clear();

                // An explicit empty entry, rather than auto-selecting the first
                // device: silently preselecting would let "Save config" persist
                // a device the user never chose.
                combo.Items.Add(NoSelection());

                foreach (AudioDeviceInfo device in devices)
                {
                    combo.Items.Add(device);
                }

                int index = IndexOfDevice(combo, savedId);
                if (index < 0 && !string.IsNullOrEmpty(savedId))
                {
                    // The saved device is unplugged or disabled. Keep it
                    // selectable so saving does not discard the user's choice.
                    string caption = Loc.T(Str.DeviceSavedUnavailable);
                    combo.Items.Add(new AudioDeviceInfo
                    {
                        Id = savedId,
                        FriendlyName = caption,
                        DisplayName = caption + " " + savedId
                    });
                    index = combo.Items.Count - 1;
                }

                // Index 0 is the "(none)" placeholder.
                combo.SelectedIndex = index < 0 ? 0 : index;
            }
            finally
            {
                combo.EndUpdate();
            }
        }

        private static int IndexOfDevice(ComboBox combo, string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                return -1;
            }

            for (int i = 0; i < combo.Items.Count; i++)
            {
                AudioDeviceInfo item = combo.Items[i] as AudioDeviceInfo;
                if (item != null && string.Equals(item.Id, deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private void OnAutoStartChanged(object sender, EventArgs e)
        {
            if (_loading)
            {
                return;
            }

            string error = AutoStartManager.SetEnabled(_chkAutoStart.Checked);
            if (error != null)
            {
                MessageBox.Show(this, Loc.T(Str.MessageAutoStartFailed, error),
                    Loc.T(Str.MessageErrorCaption),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

                _loading = true;
                _chkAutoStart.Checked = AutoStartManager.IsEnabled();
                _loading = false;
                return;
            }

            Logger.Info(_chkAutoStart.Checked
                ? Loc.T(Str.LogAutoStartEnabled, AutoStartManager.CommandLine)
                : Loc.T(Str.LogAutoStartDisabled));
        }

        private void UpdateSignatureIndicators()
        {
            UpdateSignatureIndicator(_txtSigOn, _lblSigOnCheck);
            UpdateSignatureIndicator(_txtSigOff, _lblSigOffCheck);
        }

        /// <summary>
        /// Shows whether the trailing byte really is the XOR of the preceding
        /// ones, so an edited signature can be sanity-checked before enabling
        /// checksum verification.
        /// </summary>
        private static void UpdateSignatureIndicator(TextBox box, Label label)
        {
            byte[] bytes;
            if (!Hex.TryParse(box.Text, out bytes) || bytes.Length == 0)
            {
                label.Text = Loc.T(Str.SignatureUnparseable);
                label.ForeColor = Color.Firebrick;
                return;
            }

            if (bytes.Length < 2)
            {
                label.Text = Loc.T(Str.SignatureByteCount, bytes.Length);
                label.ForeColor = Color.Gray;
                return;
            }

            byte expected = Hex.XorChecksum(bytes, bytes.Length - 1);
            if (bytes[bytes.Length - 1] == expected)
            {
                label.Text = Loc.T(Str.SignatureChecksumOk, bytes.Length);
                label.ForeColor = Color.SeaGreen;
            }
            else
            {
                label.Text = Loc.T(Str.SignatureChecksumBad, bytes.Length);
                label.ForeColor = Color.Firebrick;
            }
        }

        // ---- HID diagnostics --------------------------------------------------

        /// <summary>
        /// Lists the HID collections Windows currently exposes and offers to
        /// derive a match string from one of them - far quicker than guessing at
        /// Device Manager's instance IDs.
        /// </summary>
        private void ShowHidDeviceDialog()
        {
            List<string> instanceIds = HidDeviceLocator.EnumerateInstanceIds();

            using (Form dialog = new Form())
            using (ListBox list = new ListBox())
            using (Label hint = new Label())
            using (Button useButton = new Button())
            using (Button closeButton = new Button())
            {
                dialog.Text = Loc.T(Str.DialogHidTitle);
                dialog.Font = Font;
                // Sized for the longer of the two languages: the English hint is
                // roughly twice the width of the Chinese one.
                dialog.ClientSize = new Size(720, 400);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;

                hint.Text = Loc.T(Str.DialogHidHint, instanceIds.Count);
                hint.Location = new Point(12, 10);
                hint.Size = new Size(696, 20);
                hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                list.Location = new Point(12, 34);
                list.Size = new Size(696, 310);
                list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                              | AnchorStyles.Bottom;
                list.HorizontalScrollbar = true;
                list.Font = new Font("Consolas", 9F);
                foreach (string id in instanceIds)
                {
                    list.Items.Add(id);
                }
                if (list.Items.Count > 0)
                {
                    list.SelectedIndex = 0;
                }

                useButton.Text = Loc.T(Str.DialogUseAsMatch);
                useButton.Location = new Point(12, 356);
                useButton.Size = new Size(140, 28);
                useButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                useButton.Enabled = list.Items.Count > 0;
                useButton.Click += (sender, e) =>
                {
                    string selected = list.SelectedItem as string;
                    if (selected == null)
                    {
                        return;
                    }

                    _txtHidMatch.Text = DeriveMatchString(selected);
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };

                closeButton.Text = Loc.T(Str.DialogClose);
                closeButton.Location = new Point(628, 356);
                closeButton.Size = new Size(80, 28);
                closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                closeButton.DialogResult = DialogResult.Cancel;
                closeButton.Click += (sender, e) => dialog.Close();

                dialog.Controls.Add(hint);
                dialog.Controls.Add(list);
                dialog.Controls.Add(useButton);
                dialog.Controls.Add(closeButton);
                dialog.CancelButton = closeButton;

                dialog.ShowDialog(this);
            }
        }

        /// <summary>
        /// "HID\VID_413C&amp;PID_A529&amp;MI_05&amp;COL02\9&amp;2658857&amp;0&amp;0001"
        /// becomes "VID_413C&amp;PID_A529&amp;MI_05&amp;COL02" - the middle segment holds
        /// the stable identifiers, the last one encodes the USB port.
        /// </summary>
        private static string DeriveMatchString(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return instanceId;
            }

            string[] parts = instanceId.Split('\\');
            return parts.Length >= 2 ? parts[1] : instanceId;
        }
    }
}

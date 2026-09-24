using System;
using System.Collections.Generic;
using System.Globalization;

namespace USBDongle_AudioSwitch
{
    /// <summary>Language selected by the user, before "auto" is resolved.</summary>
    public enum AppLanguage
    {
        /// <summary>Follow the operating system's UI language.</summary>
        Auto = 0,
        English = 1,
        Chinese = 2
    }

    /// <summary>
    /// Every string that reaches a user, keyed so both languages stay in sync.
    ///
    /// Keys are grouped the way the program is, and each entry holds exactly two
    /// strings - English first, Chinese second. Adding a key without adding both
    /// translations is a compile error rather than a blank label at runtime, and
    /// a missing entry throws instead of silently falling back, so a typo shows
    /// up immediately in testing.
    /// </summary>
    internal enum Str
    {
        // ---- window, groups, buttons ---------------------------------------
        WindowTitle,
        TrayTooltip,
        GroupAudio,
        GroupHid,
        GroupLog,
        LabelHeadsetTarget,
        LabelSpeakerTarget,
        ButtonRefreshDevices,
        LabelHidMatch,
        ButtonListHid,
        LabelSignatureOn,
        LabelSignatureOff,
        LabelSignatureLength,
        LabelCooldown,
        LabelReopenDelay,
        CheckVerifyChecksum,
        CheckLogUnknownReports,
        CheckAutoStart,
        ButtonSave,
        ButtonStart,
        ButtonStop,
        ButtonClearLog,
        ButtonOpenFolder,
        LabelLanguage,

        // ---- tray menu ------------------------------------------------------
        MenuShow,
        MenuStart,
        MenuStop,
        MenuExit,

        // ---- status bar -----------------------------------------------------
        StatusReady,
        StatusMonitoring,
        StatusStopped,
        StatusSwitchedToHeadset,
        StatusSwitchedToSpeaker,
        StatusSwitchFailed,
        StatusNoTargets,
        StatusAlreadyTarget,
        StatusDeviceNotConnected,
        StatusDevicePrefix,
        StatusEventPrefix,

        // ---- device list ----------------------------------------------------
        DeviceNoneSelected,
        DeviceSavedUnavailable,
        DeviceUnnamed,

        // ---- log lines ------------------------------------------------------
        LogConfigFile,
        LogLogFile,
        LogNoTargetsConfigured,
        LogTargetsNotChosen,
        LogAutoStartStale,
        LogDevicesLoaded,
        LogLogCleared,
        LogMinimisedToTray,
        LogAutoStartEnabled,
        LogAutoStartDisabled,
        LogStartMonitoring,
        LogStartTargetOn,
        LogStartTargetOff,
        LogTargetNotConfigured,
        LogMonitoringStopped,
        LogStopMonitoring,
        LogHeadsetPowerOn,
        LogHeadsetPowerOff,
        LogAlreadyTarget,
        LogSwitchingTo,
        LogSwitchSucceeded,
        LogSwitchFailed,
        LogUnresolvedTarget,
        LogDeviceUnavailable,
        LogProgramStarted,
        LogProgramExited,
        LogTrayIconFrame,
        LogTrayIconDarkTaskbar,
        LogTrayIconLightTaskbar,
        LogThemeReadFailed,
        LogDpiReadFailed,
        LogIconResourceMissing,
        LogIconLoadFailed,

        // ---- HID monitor ----------------------------------------------------
        LogMonitorThreadStuck,
        LogHidNotFound,
        LogHidOpened,
        LogReadLoopAborted,
        LogHidReadEnded,
        LogMonitorStopped,
        LogSignalOn,
        LogSignalOff,
        LogUnmatchedReport,
        LogReadFileFailed,
        LogWaitFailed,
        LogReadFailed,
        LogSignalHandlingError,
        LogOpenHidFailed,
        LogCreateOverlappedEventFailed,

        // ---- audio switching ------------------------------------------------
        LogEnumeratePlaybackFailed,
        LogReadDeviceListFailed,
        ErrorNoTargetDeviceId,
        LogPolicyConfigFailed,
        LogTryingSoundVolumeView,
        ErrorBothStrategiesFailed,
        ErrorNativeFailedNoFallback,
        ErrorSoundVolumeViewStartFailed,
        ErrorSoundVolumeViewTimeout,

        // ---- config ---------------------------------------------------------
        LogConfigNotFound,
        LogConfigEmpty,
        LogConfigParseFailed,

        // ---- autostart ------------------------------------------------------
        ErrorRegistryKeyOpenFailed,

        // ---- dialogs and messages -------------------------------------------
        DialogHidTitle,
        DialogHidHint,
        DialogUseAsMatch,
        DialogClose,
        MessageErrorCaption,
        MessageInvalidConfigCaption,
        MessageOpenFolderFailed,
        MessageSaveFailed,
        MessageAutoStartFailed,
        ErrorHidMatchEmpty,
        ErrorSignatureOnUnparseable,
        ErrorSignatureOffUnparseable,
        ErrorSignatureTooLong,
        SignatureUnparseable,
        SignatureByteCount,
        SignatureChecksumOk,
        SignatureChecksumBad,
        MessageStillRunningTitle,
        MessageStillRunningBody,

        // ---- single instance ------------------------------------------------
        FatalUiThreadException,
        FatalUnhandledException,

        // ---- language picker ------------------------------------------------
        LanguageAuto,
        LanguageEnglish,
        LanguageChinese
    }

    internal static class Loc
    {
        private const int En = 0;
        private const int Zh = 1;

        private static readonly Dictionary<Str, string[]> Table =
            new Dictionary<Str, string[]>
        {
            // ---- window, groups, buttons -----------------------------------
            { Str.WindowTitle, new[] {
                "USB Wireless Headset Audio Auto-Switch",
                "USB 无线耳机音频自动切换" } },
            { Str.TrayTooltip, new[] {
                "USB Headset Audio Switch",
                "USB 耳机音频自动切换" } },
            { Str.GroupAudio, new[] { "Audio devices", "音频设备" } },
            { Str.GroupHid, new[] { "HID detection", "HID 检测配置" } },
            { Str.GroupLog, new[] { "Log", "运行日志" } },
            // Kept short on purpose: the long "switch to when ..." phrasing runs
            // 179px, wider than the label column, and would overlap the
            // drop-down. The group title and the device list carry the rest.
            { Str.LabelHeadsetTarget, new[] {
                "When headset is on:",
                "耳机开机时切换到：" } },
            { Str.LabelSpeakerTarget, new[] {
                "When headset is off:",
                "耳机关机时切换到：" } },
            { Str.ButtonRefreshDevices, new[] { "Refresh devices", "刷新设备列表" } },
            { Str.LabelHidMatch, new[] { "HID device match:", "HID 设备匹配串：" } },
            { Str.ButtonListHid, new[] { "List HID devices", "列出当前 HID 设备" } },
            { Str.LabelSignatureOn, new[] { "Power-on signature:", "开机签名字节：" } },
            { Str.LabelSignatureOff, new[] { "Power-off signature:", "关机签名字节：" } },
            { Str.LabelSignatureLength, new[] { "Signature length:", "签名长度：" } },
            { Str.LabelCooldown, new[] { "Cooldown (ms):", "冷却 (ms)：" } },
            { Str.LabelReopenDelay, new[] { "Reopen delay (ms):", "重开延迟 (ms)：" } },
            { Str.CheckVerifyChecksum, new[] {
                "Verify trailing XOR checksum",
                "校验末字节 XOR 校验和" } },
            { Str.CheckLogUnknownReports, new[] {
                "Log unmatched reports",
                "记录未匹配的报告" } },
            { Str.CheckAutoStart, new[] {
                "Start with Windows (HKCU Run)",
                "开机自启动（写入 HKCU Run）" } },
            { Str.ButtonSave, new[] { "Save config", "保存配置" } },
            { Str.ButtonStart, new[] { "Start monitoring", "开始监控" } },
            { Str.ButtonStop, new[] { "Stop monitoring", "停止监控" } },
            { Str.ButtonClearLog, new[] { "Clear log", "清空日志" } },
            { Str.ButtonOpenFolder, new[] { "Config folder", "配置目录" } },
            { Str.LabelLanguage, new[] { "Language:", "界面语言：" } },

            // ---- tray menu --------------------------------------------------
            { Str.MenuShow, new[] { "Show window", "显示主界面" } },
            { Str.MenuStart, new[] { "Start monitoring", "开始监控" } },
            { Str.MenuStop, new[] { "Stop monitoring", "停止监控" } },
            { Str.MenuExit, new[] { "Exit", "退出" } },

            // ---- status bar -------------------------------------------------
            { Str.StatusReady, new[] { "Ready", "就绪" } },
            { Str.StatusMonitoring, new[] { "Monitoring", "监控中" } },
            { Str.StatusStopped, new[] { "Stopped", "已停止" } },
            { Str.StatusSwitchedToHeadset, new[] {
                "Switched to headset",
                "已切换到耳机" } },
            { Str.StatusSwitchedToSpeaker, new[] {
                "Switched to speaker",
                "已切换到扬声器" } },
            { Str.StatusSwitchFailed, new[] { "Switch failed", "切换失败" } },
            { Str.StatusNoTargets, new[] {
                "No target device configured",
                "未配置目标设备" } },
            { Str.StatusAlreadyTarget, new[] {
                "Already the target device",
                "已是目标设备" } },
            { Str.StatusDeviceNotConnected, new[] {
                "Device: not connected",
                "设备：未连接" } },
            { Str.StatusDevicePrefix, new[] { "Device: ", "设备：" } },
            { Str.StatusEventPrefix, new[] { "Events: ", "事件：" } },

            // ---- device list ------------------------------------------------
            { Str.DeviceNoneSelected, new[] { "(none)", "(未选择)" } },
            { Str.DeviceSavedUnavailable, new[] {
                "(saved, currently unavailable)",
                "(已保存，当前不可用)" } },
            { Str.DeviceUnnamed, new[] { "(unnamed device)", "(未命名设备)" } },

            // ---- log lines --------------------------------------------------
            { Str.LogConfigFile, new[] { "Config file: ", "配置文件：" } },
            { Str.LogLogFile, new[] { "Log file: ", "日志文件：" } },
            { Str.LogNoTargetsConfigured, new[] {
                "No headset/speaker target configured yet - pick them on the main "
                + "window and click \"Save config\"",
                "尚未配置耳机/扬声器目标设备，请在主界面选择后点击“保存配置”" } },
            { Str.LogAutoStartStale, new[] {
                "The autostart entry points at a different executable - tick "
                + "\"Start with Windows\" again to fix it",
                "开机自启动项指向的可执行文件路径已变化，请重新勾选“开机自启动”以修正" } },
            { Str.LogTargetsNotChosen, new[] {
                "A target device is still unset, so a detected power change will "
                + "not switch anything. Pick one in the drop-downs on the main "
                + "window and click \"Start monitoring\" again",
                "还有目标设备未选择，检测到开关机信号时不会执行切换。"
                + "请在主界面的下拉框中选择后重新点击“开始监控”" } },
            { Str.LogDevicesLoaded, new[] {
                "Loaded {0} playback device(s)",
                "已加载 {0} 个播放设备" } },
            { Str.LogLogCleared, new[] { "Log cleared", "日志已清空" } },
            { Str.LogMinimisedToTray, new[] {
                "Main window minimised to the tray, monitoring continues",
                "主窗口已最小化到托盘，监控继续运行" } },
            { Str.LogAutoStartEnabled, new[] {
                "Autostart enabled: {0}",
                "已启用开机自启动：{0}" } },
            { Str.LogAutoStartDisabled, new[] {
                "Autostart disabled",
                "已取消开机自启动" } },
            { Str.LogStartMonitoring, new[] {
                "Start monitoring: match \"{0}\", signature length {1}, cooldown {2} ms",
                "开始监控：匹配串 \"{0}\"，签名长度 {1}，冷却 {2} ms" } },
            { Str.LogStartTargetOn, new[] { "  on  -> {0}", "  开机 → {0}" } },
            { Str.LogStartTargetOff, new[] { "  off -> {0}", "  关机 → {0}" } },
            { Str.LogTargetNotConfigured, new[] {
                "{0}, but no target device is configured - pick one on the main "
                + "window and retry",
                "{0}，但尚未配置目标设备，请在主界面选择后重试" } },
            { Str.LogMonitoringStopped, new[] {
                "Monitoring stopped",
                "已停止监控" } },
            { Str.LogStopMonitoring, new[] { "Monitoring stopped", "已停止监控" } },
            { Str.LogHeadsetPowerOn, new[] { "Headset <on>", "耳机开机" } },
            { Str.LogHeadsetPowerOff, new[] { "Headset <off>", "耳机关机" } },
            { Str.LogAlreadyTarget, new[] {
                "{0}, the current default device is already the target - nothing to do",
                "{0}，当前默认设备已是目标设备，无需切换" } },
            { Str.LogSwitchingTo, new[] {
                "Switching default playback device -> {0}",
                "切换默认播放设备 → {0}" } },
            { Str.LogSwitchSucceeded, new[] {
                "Switch succeeded (report: {0})",
                "切换成功（报告：{0}）" } },
            { Str.LogSwitchFailed, new[] { "Switch failed: {0}", "切换失败：{0}" } },
            { Str.LogUnresolvedTarget, new[] { "(not configured)", "(未配置)" } },
            { Str.LogDeviceUnavailable, new[] {
                "{0}  (device currently unavailable)",
                "{0}  (设备当前不可用)" } },
            { Str.LogProgramStarted, new[] {
                "========== Program started ==========",
                "========== 程序启动 ==========" } },
            { Str.LogProgramExited, new[] {
                "========== Program exited ==========",
                "========== 程序退出 ==========" } },
            { Str.LogTrayIconFrame, new[] {
                "Tray icon: the tray wants {0}px, loading the {1}px frame",
                "托盘图标：托盘需要 {0}px，加载 {1}px 帧" } },
            { Str.LogTrayIconDarkTaskbar, new[] {
                "Tray icon: dark taskbar, using the white artwork",
                "托盘图标：任务栏为深色，使用白色图案" } },
            { Str.LogTrayIconLightTaskbar, new[] {
                "Tray icon: light taskbar, using the black artwork",
                "托盘图标：任务栏为浅色，使用黑色图案" } },
            { Str.LogThemeReadFailed, new[] {
                "Could not read the taskbar theme, assuming a dark taskbar: {0}",
                "读取任务栏主题失败，按深色任务栏处理：{0}" } },
            { Str.LogDpiReadFailed, new[] {
                "Could not read the system DPI, loading the tray icon at its default "
                + "size: {0}",
                "读取系统 DPI 失败，托盘图标按默认尺寸加载：{0}" } },
            { Str.LogIconResourceMissing, new[] {
                "Icon not found among the embedded resources: {0}",
                "嵌入资源中未找到图标：{0}" } },
            { Str.LogIconLoadFailed, new[] {
                "Loading the icon failed: {0}",
                "加载图标失败：{0}" } },

            // ---- HID monitor ------------------------------------------------
            { Str.LogMonitorThreadStuck, new[] {
                "The monitor thread did not exit within 5 seconds; it will finish on its own",
                "监控线程未能在 5 秒内退出，将在后台自行结束" } },
            { Str.LogHidNotFound, new[] {
                "No HID device found (match \"{0}\"), retrying in {1} ms",
                "未找到 HID 设备（匹配串 \"{0}\"），{1} ms 后重试" } },
            { Str.LogHidOpened, new[] { "HID device opened: {0}", "HID 设备已打开：{0}" } },
            { Str.LogReadLoopAborted, new[] {
                "Read loop aborted: {0}",
                "读取循环异常终止：{0}" } },
            { Str.LogHidReadEnded, new[] {
                "HID read ended (disconnected or unplugged), reopening in {0} ms",
                "HID 设备读取结束（已断开或被拔出），{0} ms 后重新打开" } },
            { Str.LogMonitorStopped, new[] { "Monitoring stopped", "监控已停止" } },
            { Str.LogSignalOn, new[] {
                "Detected headset <on>: {0}",
                "检测到耳机 <开机>：{0}" } },
            { Str.LogSignalOff, new[] {
                "Detected headset <off>: {0}",
                "检测到耳机 <关机>：{0}" } },
            { Str.LogUnmatchedReport, new[] {
                "Unmatched report (length {0}): {1}",
                "未匹配的报告（长度 {0}）：{1}" } },
            { Str.LogReadFileFailed, new[] {
                "ReadFile failed ({0}); the device may be gone, reopening",
                "ReadFile 失败（{0}），设备可能已移除，重新打开" } },
            { Str.LogWaitFailed, new[] {
                "Waiting for the HID read to complete failed ({0})",
                "等待 HID 读取完成失败（{0}）" } },
            { Str.LogReadFailed, new[] {
                "HID read failed ({0}); the device may be gone, reopening",
                "HID 读取失败（{0}），设备可能已移除，重新打开" } },
            { Str.LogSignalHandlingError, new[] {
                "Error while handling the HID signal",
                "处理 HID 信号时出错" } },
            { Str.LogOpenHidFailed, new[] {
                "Opening the HID device failed ({0}): {1}",
                "打开 HID 设备失败（{0}）：{1}" } },
            { Str.LogCreateOverlappedEventFailed, new[] {
                "Creating the overlapped I/O event failed ({0})",
                "创建重叠 I/O 事件失败（{0}）" } },

            // ---- audio switching --------------------------------------------
            { Str.LogEnumeratePlaybackFailed, new[] {
                "Enumerating playback devices failed, HRESULT=0x{0}",
                "枚举播放设备失败，HRESULT=0x{0}" } },
            { Str.LogReadDeviceListFailed, new[] {
                "Error while reading the playback device list",
                "读取播放设备列表时出错" } },
            { Str.ErrorNoTargetDeviceId, new[] {
                "No target device ID configured",
                "未配置目标设备 ID" } },
            { Str.LogPolicyConfigFailed, new[] {
                "IPolicyConfig switch failed ({0})",
                "IPolicyConfig 切换失败（{0}）" } },
            { Str.LogTryingSoundVolumeView, new[] {
                "Trying the SoundVolumeView fallback: {0}",
                "尝试使用 SoundVolumeView 回退方案：{0}" } },
            { Str.ErrorBothStrategiesFailed, new[] {
                "Native interface failed ({0}) and SoundVolumeView failed too ({1})",
                "原生接口失败（{0}），SoundVolumeView 也失败（{1}）" } },
            { Str.ErrorNativeFailedNoFallback, new[] {
                "Native interface failed: {0}. You can point soundVolumeViewPath at "
                + "SoundVolumeView.exe as a fallback.",
                "原生接口失败：{0}。可在配置中指定 SoundVolumeView.exe 作为回退。" } },
            { Str.ErrorSoundVolumeViewStartFailed, new[] {
                "Could not start SoundVolumeView",
                "无法启动 SoundVolumeView" } },
            { Str.ErrorSoundVolumeViewTimeout, new[] {
                "SoundVolumeView timed out (role={0})",
                "SoundVolumeView 超时 (role={0})" } },

            // ---- config -----------------------------------------------------
            { Str.LogConfigNotFound, new[] {
                "No config file found, using defaults: {0}",
                "未找到配置文件，使用默认配置：{0}" } },
            { Str.LogConfigEmpty, new[] {
                "Config file is empty, using defaults",
                "配置文件为空，使用默认配置" } },
            { Str.LogConfigParseFailed, new[] {
                "Config file could not be parsed; backed it up and fell back to defaults",
                "配置文件解析失败，已备份并回退到默认配置" } },

            // ---- autostart --------------------------------------------------
            { Str.ErrorRegistryKeyOpenFailed, new[] {
                "Could not open the registry key HKCU\\{0}",
                "无法打开注册表项 HKCU\\{0}" } },

            // ---- dialogs and messages ---------------------------------------
            { Str.DialogHidTitle, new[] {
                "HID devices on this system",
                "当前系统的 HID 设备" } },
            { Str.DialogHidHint, new[] {
                "Select a row and click \"Use as match string\" to fill in the "
                + "VID/PID/MI/COL part automatically. ({0} device(s))",
                "选中一行后点击“用作匹配串”，会自动截取 VID/PID/MI/COL 部分填入配置。"
                + "（共 {0} 个设备）" } },
            { Str.DialogUseAsMatch, new[] { "Use as match string", "用作匹配串" } },
            { Str.DialogClose, new[] { "Close", "关闭" } },
            { Str.MessageErrorCaption, new[] { "Error", "错误" } },
            { Str.MessageInvalidConfigCaption, new[] { "Invalid config", "配置无效" } },
            { Str.MessageOpenFolderFailed, new[] {
                "Could not open the config folder: {0}",
                "无法打开配置目录：{0}" } },
            { Str.MessageSaveFailed, new[] {
                "Saving the config failed: {0}",
                "保存配置失败：{0}" } },
            { Str.MessageAutoStartFailed, new[] {
                "Setting the autostart entry failed: {0}",
                "设置开机自启动失败：{0}" } },
            { Str.ErrorHidMatchEmpty, new[] {
                "The HID device match string cannot be empty.",
                "HID 设备匹配串不能为空。" } },
            { Str.ErrorSignatureOnUnparseable, new[] {
                "The power-on signature could not be parsed. Use hexadecimal like "
                + "\"08 c0 09 03 00 01 cc 0f\".",
                "开机签名字节无法解析。请使用形如 “08 c0 09 03 00 01 cc 0f” 的十六进制。" } },
            { Str.ErrorSignatureOffUnparseable, new[] {
                "The power-off signature could not be parsed. Use hexadecimal like "
                + "\"08 c0 09 03 00 01 dd 1e\".",
                "关机签名字节无法解析。请使用形如 “08 c0 09 03 00 01 dd 1e” 的十六进制。" } },
            { Str.ErrorSignatureTooLong, new[] {
                "Signature length is {0}, but the shorter signature only has {1} byte(s).",
                "签名长度为 {0}，但较短的签名字节只有 {1} 个字节。" } },
            { Str.SignatureUnparseable, new[] { "Unparseable", "无法解析" } },
            { Str.SignatureByteCount, new[] { "{0} bytes", "{0} 字节" } },
            { Str.SignatureChecksumOk, new[] {
                "{0} bytes, checksum ✓",
                "{0} 字节，校验和 ✓" } },
            { Str.SignatureChecksumBad, new[] {
                "{0} bytes, checksum ✗",
                "{0} 字节，校验和 ✗" } },
            { Str.MessageStillRunningTitle, new[] {
                "Still running in the background",
                "仍在后台运行" } },
            { Str.MessageStillRunningBody, new[] {
                "The program has been minimised to the tray and keeps watching for "
                + "the headset powering on and off. Double-click the tray icon to "
                + "open it again.",
                "程序已最小化到托盘，会继续监听耳机开关机。双击托盘图标可重新打开。" } },

            // ---- single instance --------------------------------------------
            { Str.FatalUiThreadException, new[] {
                "UI thread exception",
                "UI 线程异常" } },
            { Str.FatalUnhandledException, new[] {
                "Unhandled exception",
                "未处理异常" } },

            // ---- language picker --------------------------------------------
            // Each language names itself, so the entry stays findable no matter
            // which language the window is currently in.
            { Str.LanguageAuto, new[] {
                "Auto (follow system)",
                "自动（跟随系统）" } },
            { Str.LanguageEnglish, new[] { "English", "English" } },
            { Str.LanguageChinese, new[] { "中文", "中文" } }
        };

        /// <summary>The language actually in use; <see cref="AppLanguage.Auto"/> is already resolved.</summary>
        public static AppLanguage Current { get; private set; }

        /// <summary>
        /// Picks the language to display, resolving <see cref="AppLanguage.Auto"/>
        /// against the OS UI language.
        ///
        /// An unrecognised value falls back to Auto rather than throwing: the
        /// value can come from a hand-edited config.json, and refusing to start
        /// over a bad language code would be absurd.
        /// </summary>
        public static void Apply(AppLanguage requested)
        {
            switch (requested)
            {
                case AppLanguage.English:
                case AppLanguage.Chinese:
                    Current = requested;
                    break;
                default:
                    Current = DetectSystemLanguage();
                    break;
            }
        }

        private static AppLanguage DetectSystemLanguage()
        {
            CultureInfo ui = CultureInfo.CurrentUICulture;
            bool chinese = ui.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);

            return chinese ? AppLanguage.Chinese : AppLanguage.English;
        }

        /// <summary>
        /// The string for <paramref name="key"/> in the current language.
        ///
        /// <paramref name="args"/> are formatted with the invariant culture: the
        /// placeholders are only ever counts, durations and hex dumps, and
        /// letting a locale reformat them would make log lines harder to compare.
        /// </summary>
        public static string T(Str key, params object[] args)
        {
            string[] entry;
            if (!Table.TryGetValue(key, out entry))
            {
                // A missing entry is a programming error, not user input. Say so
                // loudly instead of rendering an empty label that looks fine.
                throw new ArgumentOutOfRangeException(
                    "Str", key, "No localization entry for " + key);
            }

            string text = entry[Current == AppLanguage.Chinese ? Zh : En];
            return args == null || args.Length == 0
                ? text
                : string.Format(CultureInfo.InvariantCulture, text, args);
        }

        /// <summary>Parses a config-file value into a language, defaulting to Auto.</summary>
        public static AppLanguage Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AppLanguage.Auto;
            }

            string normalized = value.Trim();
            if (normalized.Equals("en", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("english", StringComparison.OrdinalIgnoreCase))
            {
                return AppLanguage.English;
            }
            if (normalized.Equals("zh", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("cn", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("chinese", StringComparison.OrdinalIgnoreCase))
            {
                return AppLanguage.Chinese;
            }
            return AppLanguage.Auto;
        }

        /// <summary>The config-file form of a language, round-tripping through <see cref="Parse"/>.</summary>
        public static string ToConfigValue(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.English: return "en";
                case AppLanguage.Chinese: return "zh";
                default: return "auto";
            }
        }
    }
}

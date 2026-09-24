# USB Wireless Headset Audio Auto-Switch

**[中文文档 → README_CN.md](README_CN.md)**

Installed as **USBDongle AudioSwitch** — the name Windows shows in Apps &
features, in the Start menu and in Task Manager is that one, in English, whatever
language the program itself is running in.

Watches the HID status reports a USB wireless headset's dongle emits, and switches
the Windows default playback device to the headset when it powers **on** and back
to the speakers when it powers **off**. No manual switching at any point.

This is a C# rewrite of `audio_switch.ps1`: same behaviour, same built-in
signatures, but no resident PowerShell process and no dependency on
`Get-PnpDevice` or an external `SoundVolumeView.exe`. The original script is still
in the repository for reference and is copied to the output directory by the
build.

> Inspired by [Meladon90/Audio-Switch](https://github.com/Meladon90/Audio-Switch).

---
## Tested Devices:
Alienware Pro Headset

## Contents

- [How it works](#how-it-works)
- [Requirements](#requirements)
- [Building](#building)
- [Packaging an installer (MSI)](#packaging-an-installer-msi)
- [Running](#running)
- [Using the window](#using-the-window)
- [Configuration file](#configuration-file)
- [File locations](#file-locations)
- [Starting with Windows](#starting-with-windows)
- [HID signatures and the checksum](#hid-signatures-and-the-checksum)
- [Troubleshooting](#troubleshooting)
- [Project layout](#project-layout)

---

## How it works

```
USB dongle ──HID input report──▶ HidMonitor ──signature match──▶ MonitorController
                                                                        │
                                                        ┌───────────────┴───────────────┐
                                                    power-on                        power-off
                                                     signature                       signature
                                                        │                               │
                                                switch to headset               switch to speaker
                                                        └───────────▶ AudioSwitcher ◀───┘
                                                                   (IPolicyConfig COM)
```

1. **Locate the device** — `HidDeviceLocator` uses SetupAPI to enumerate the HID
   collections currently present and takes the first whose instance ID contains the
   match string (default `VID_413C&PID_A529&MI_05&COL02`). The device path is
   **re-resolved every time it is opened**, so moving the dongle to another USB
   port or unplugging it recovers on its own with no config change.
2. **Read reports** — `HidMonitor` blocks on input reports using overlapped I/O
   (`FILE_FLAG_OVERLAPPED`) with a 256-byte buffer.
3. **Match a signature** — the first `signatureLength` (default 8) bytes of the
   report are compared byte-for-byte against the power-on and power-off
   signatures. **Power-off is tested before power-on.** A hit starts a cooldown
   (default 1500 ms), which collapses the burst of reports a power-on produces
   into a single switch.
4. **Switch device** — `AudioSwitcher` calls the undocumented but stable-since-Win7
   `IPolicyConfig` COM interface (`CPolicyConfigClient`) — the same interface
   `SoundVolumeView.exe` drives internally. By default it sets all three roles,
   **Console / Multimedia / Communications**, matching the original script.
5. **Fall back on failure** — if the COM call fails and `soundVolumeViewPath` is
   set, it falls back to `SoundVolumeView.exe /SetDefault`.

Before switching, the current default device is read **live** and compared; if it
is already the target, the switch is skipped rather than repeated.

---

## Requirements

| Item | Requirement |
| --- | --- |
| Operating system | Windows 7 or later (tested on 64-bit Windows 10/11) |
| Runtime | .NET Framework **4.8** |
| Build | MSBuild (from Visual Studio, the Build Tools, or the one bundled with JetBrains Rider) |
| Optional | `SoundVolumeView.exe` (only if the native COM call is blocked) |

.NET Framework 4.8 ships with Windows 10 (1903+) and Windows 11, so on a current
machine nothing needs to be installed. On older systems it comes from the
[.NET Framework 4.8 runtime download](https://dotnet.microsoft.com/download/dotnet-framework/net48).

> **This program does not use .NET 8, .NET 6 or any .NET Core / .NET 5+ runtime.**
> It is a .NET Framework 4.8 application, and `USBDongle_AudioSwitch.exe.config`
> declares exactly that. If Windows asks for a ".NET Desktop Runtime" when
> something starts at sign-in, that prompt is coming from a different program on
> the machine — installing a .NET 8 runtime will not change this one's behaviour.

The .NET Framework 4.8 **targeting pack is not required** — when it is missing,
`build.ps1` automatically compiles against the runtime assemblies instead. See
[Building](#building).

---

## Building

```powershell
cd USBDongle_AudioSwitch
powershell -File build.ps1
```

Output: `bin\Release\USBDongle_AudioSwitch.exe`

### Parameters

| Parameter | Meaning |
| --- | --- |
| `-Configuration Debug` | Build Debug instead of the default `Release` |
| `-MSBuildPath <path>` | Use this `MSBuild.exe`, skipping the automatic search |
| `-NoFrameworkPathOverride` | Do **not** fall back when the targeting pack is missing, so real environment problems surface |

### MSBuild search order

1. The path given by `-MSBuildPath`
2. A Visual Studio / Build Tools installation (via `vswhere.exe`)
3. `msbuild` on `PATH`
4. The MSBuild bundled with JetBrains Rider (scoop or Toolbox installs)

### About the targeting pack

Compiling a .NET Framework 4.8 project normally needs the targeting pack, which
ships with the Windows SDK and is absent on many machines. When the targeting pack
is missing but the 4.8 **runtime** is present, the program runs fine — it just
will not compile.

`build.ps1` detects this and passes:

```
/p:FrameworkPathOverride=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
```

so MSBuild compiles against the installed runtime assemblies instead. The script
prints which path it actually used.

---

## Packaging an installer (MSI)

```powershell
cd USBDongle_AudioSwitch
powershell -File build-msi.ps1
```

Output: `dist\USBDongle_AudioSwitch-<version>.msi`. The script calls `build.ps1`
first and then packages with WiX; `-SkipApp` skips the compile and packages
whatever is already in `bin\`.

### Prerequisites

```powershell
scoop install wixtoolset versions/dotnet-sdk-lts
wix eula accept wix7      # WiX 7 needs the OSMF terms accepted once
```

### Installer behaviour

| Item | Behaviour |
| --- | --- |
| Name Windows shows | `USBDongle AudioSwitch` — in Apps & features, the Start menu folder, both shortcuts, and the exe's Properties → Details |
| Administrator rights | **Not required, no UAC prompt** |
| Install location | `%LOCALAPPDATA%\Programs\USBDongle_AudioSwitch` |
| Shortcuts | Start menu and desktop, both pointing at the installed exe |
| Uninstall | Listed under "Apps & features", or `msiexec /x` |
| Upgrade | Fixed UpgradeCode, so it installs straight over an older version |
| Silent install | `msiexec /i <msi> /qn` |

The install-time name is **fixed English**, unlike everything inside the program:
it is a product name rather than UI text, so it does not follow the in-app
language switch. The `.wxs` spells it in three places (`Package/@Name`, the Start
Menu folder, the two `Shortcut/@Name`) exactly so they cannot drift apart. The
window title and the tray tooltip are separate, localized strings — see
[Using the window](#using-the-window).

**On uninstall** it clears the HKCU autostart entry (otherwise a dead entry
pointing at a deleted exe would be left behind) but **keeps** the configuration
and logs under `%APPDATA%`, so settings survive a reinstall. Removing the
autostart entry is done by a custom action: that registry value is written by the
program at runtime and the MSI does not "own" it, so the standard component rules
cannot reclaim it.

> One known detail: under an **administrator account** the "Apps & features" entry
> lands in `HKLM` rather than `HKCU`, making it visible to other users on the
> machine. That is documented Windows Installer behaviour for `ALLUSERS=2`
> (register machine-wide when permitted); the install itself still needs no
> elevation and the files still go under the user's profile. Under a standard user
> account it registers in `HKCU`.

### Packaging gotchas

The relevant places in the `.wxs` are commented — worth reading before changing
anything, because WiX 4+ differs from v3 in ways that are not obvious:

- `Assembly=".net"` in WiX 4+ means **install into the GAC** (in v3 it meant
  "compare by assembly version"), and rejects assemblies without a strong name.
- `CustomAction` no longer has an `Exe` attribute; the whole command line goes in
  `ExeCommand`.
- **String literals in MSI conditions must use double quotes.** Writing
  `REMOVE~='ALL'` with single quotes makes Windows Installer reject the action
  with error 2717, and the log only says the condition is wrong — never that the
  quoting is.
- `Scope="perUser"` already sets the "UAC compliant" bit (Word Count bit 3) in the
  summary information. Do **not** also hand-write `ALLUSERS=2` /
  `MSIINSTALLPERUSER=1`: MSI rejects the latter and deletes the former, which only
  adds noise to the log.

---

## Running

```powershell
& '.\bin\Release\USBDongle_AudioSwitch.exe'
```

| Command-line argument | Meaning |
| --- | --- |
| (none) | Start normally with the main window visible |
| `--tray` | Start minimised to the tray without showing the window; this is the form written into the autostart entry |

### Single instance

A `Local\USBDongle_AudioSwitch.SingleInstance` mutex keeps it to one instance.
Launching it again does not start a second process — it **activates the existing
window** instead (signalled through the `Local\USBDongle_AudioSwitch.Activate`
named event, received by a background thread).

### Tray behaviour

- **Closing the main window** minimises to the tray; monitoring continues (a
  balloon tip says so the first time).
- The only way to really quit is **tray right-click → Exit**.
- Double-clicking the tray icon reopens the main window.

---

## Using the window

### Audio devices

| Control | Meaning |
| --- | --- |
| When headset is on | The playback device to switch to when the headset powers on |
| When headset is off | The playback device to switch back to when it powers off (usually speakers or the monitor) |
| Refresh devices | Re-enumerate the active playback devices |

The first entry in each drop-down is **"(none)"**, and nothing is preselected
automatically — that avoids "Save config" writing a device the user never chose.
If the saved device is currently unavailable (unplugged or disabled) it appears at
the end of the list as **"(saved, currently unavailable)"** and is selected, so
saving does not discard the choice.

> **Note**: the list contains **active playback (render) endpoints only** — no
> recording devices.

### HID detection

| Control | Default | Meaning |
| --- | --- | --- |
| HID device match | `VID_413C&PID_A529&MI_05&COL02` | Substring of the device instance ID, matched case-insensitively. It **deliberately omits** the trailing `9&2658857&0&0001` segment, which encodes the USB port and changes when you move it |
| List HID devices | — | Opens a dialog listing the instance IDs of every HID collection on the system; select a row and click "Use as match string" to fill in the `VID/PID/MI/COL` part |
| Power-on signature | `08 c0 09 03 00 01 cc 0f` | Leading bytes of the report when the headset powers on |
| Power-off signature | `08 c0 09 03 00 01 dd 1e` | Leading bytes of the report when it powers off |
| Signature length | 8 | How many bytes take part in matching (1–64) |
| Cooldown (ms) | 1500 | How long repeats are ignored after a match |
| Reopen delay (ms) | 2000 | How long to wait before reopening the device after a read failure |
| Verify trailing XOR checksum | off | See [HID signatures and the checksum](#hid-signatures-and-the-checksum) |
| Log unmatched reports | off | Also log reports that match nothing, for diagnosing signatures |

Next to each signature box, the parse result and checksum state are shown **live**
(`8 bytes, checksum ✓`), so an edit can be verified immediately.

Signature input is forgiving: `0x` prefixes, commas, dashes, newlines and tabs are
all ignored, so a dump pasted straight out of a serial tool parses fine.

### Actions

| Button | Meaning |
| --- | --- |
| Start with Windows (HKCU Run) | Tick or untick the autostart entry |
| Save config | Validate the input and write `config.json` |
| Start monitoring | **Save the config first**, then start monitoring |
| Stop monitoring | Stop the monitor thread |
| Clear log | Clear the on-screen log and delete the log file |
| Config folder | Open `%APPDATA%\USBDongle_AudioSwitch` in Explorer |

> "Start monitoring" also saves the config. A monitor that is already running is
> **stopped and restarted**, because the settings are snapshotted when the monitor
> thread starts.

### Language

The language picker at the right-hand end of the status bar switches the whole
interface between **English and Chinese**, live and without a restart. The setting
is written to `config.json` as `language`, so it survives a restart. The default,
**Auto (follow system)**, picks Chinese on a Chinese-language Windows and English
everywhere else.

Both the interface text and everything written to the log follow the selected
language, so a log file can contain lines in both if the language is changed
part-way through.

### Status bar

On the left is the current state (`Ready` / `Monitoring` / `Stopped` /
`Switched to headset` / `Switched to speaker` / `Switch failed` /
`No target device configured` / `Already the target device`), in the middle the
currently open HID device path (`Device: not connected` when there is none), and
on the right the running count of detected signal events, followed by the language
picker.

### Log

The on-screen log and the log file carry the same content (driven by the
`Logger.LineWritten` event). The window keeps at most **2000 lines**, trimming to
half when it overflows; the log file rotates at **5 MB**.

The log box is deliberately short, and is meant to be **scrolled with the wheel**:

- **Hovering is enough to scroll it** — no need to click in first. Windows sends
  wheel messages to the window with *focus* rather than the one under the pointer,
  and a read-only log box rarely has focus, so `LogWheelRouter` re-posts the
  message to it.
- **Scrolling back through history is not interrupted by new lines.** New lines
  only auto-scroll when the view was already at the end; once you scroll back, the
  position is held until you scroll to the bottom again.

### Icons

Three icons ship, and they are deliberately different:

| File | Artwork | Used for |
| --- | --- | --- |
| `Resources/app.ico` | Black line art on an **opaque white** plate | Window, taskbar button, Alt-Tab, Explorer, the exe itself |
| `Resources/tray-white.ico` | White line art on transparency | Notification area on a **dark** taskbar |
| `Resources/tray-black.ico` | Black line art on transparency | Notification area on a **light** taskbar |

The opaque white plate is what lets one file serve the window on both Windows
themes: against a **dark** taskbar the plate reads as a white icon, and against a
**light** one the plate blends into the background so only the black drawing is
left. No theme detection is involved there.

The notification area needs real transparency instead, because it is composited
over the taskbar — an opaque plate there would sit in the tray as a white square.
That leaves the drawing itself to carry the contrast, so **the tray icon follows
the taskbar theme**: `AppIcons` reads
`HKCU\…\Themes\Personalize\SystemUsesLightTheme` and picks the white file for a
dark taskbar and the black one for a light one. It is the *System* value rather
than *Apps*UseLightTheme because Windows lets the two differ, and it is the
system one that decides how the taskbar is drawn. A machine where the value has
never been set (Windows 7, Windows 8.1, a fresh profile) reads as dark, which is
what the program drew unconditionally before.

That covers Windows' own light/dark mode, which is what the tray follows. It does
**not** cover Windows 11's *accent colour on the taskbar*: a pale accent leaves
`SystemUsesLightTheme` at 0 while the bar itself is drawn light, and the icon
stays white. Detecting that would mean second-guessing the shell's own contrast
maths, which is a worse trade than the one case it misses.

`MainForm.WndProc` watches for the `WM_SETTINGCHANGE` broadcast naming
`ImmersiveColorSet` — how the shell announces a theme change — and swaps the icon
in place without interrupting monitoring. The two tray files hold the same drawing
at the same sizes, so a swap changes the ink colour and nothing else.

Small sizes use a **separately drawn simplified glyph**, not a shrunk copy of the
original: the ear cups span only about 22% of the source image's width, which at
16px is under 4 pixels and turns into an unreadable blob that reads as a face. So
**≤32px** uses a recomposed, bolder shape (large ear cups, no interior lines, no
power symbol at 16px), and **≥40px** uses the original line art.

The tray icon is sized against the display's **real DPI**. The process is
DPI-unaware by design — Windows bitmap-scales the whole window on a high-DPI
display rather than rendering it tiny — but an unaware thread is told every
display runs at 96 DPI, so `SystemInformation.SmallIconSize` answers 16×16 even on
a 150% display whose tray slot is really 24×24, and the shell ends up stretching a
16px icon. `AppIcons` measures the true system DPI and picks the smallest frame
that still covers the slot, so the icon is drawn 1:1 or scaled *down*, never up.

Icons are generated by `assets/make_icon.py` (needs Python + Pillow) into
`Resources/`:

```bash
python assets/make_icon.py
```

The art masters live in `assets/art-black.ico` and `assets/art-white.ico`. The
original 2048px JPEG the masters were traced from is not carried in the
repository; when it is absent — the normal case — the script reuses the artwork
already in the masters, so a rebuild is byte-stable. Drop the JPEG back into
`assets/` to regenerate the large sizes from the original drawing instead.

Note that the script writes the `.ico` container by hand rather than using
Pillow's `ICO` save: Pillow rescales a single base image into every requested
size, which would throw away the "different artwork for small sizes" idea, and it
writes PNG frames by default, which `System.Drawing.Icon` handles unreliably.

---

## Configuration file

Location: `%APPDATA%\USBDongle_AudioSwitch\config.json`

If the file does not exist the defaults are used (identical to the original
script's behaviour). If it fails to parse it is backed up as `config.json.bad` and
the defaults are used. Writing is **write `.tmp`, then replace**, so a crash
mid-write cannot leave half a file behind.

A UTF-8 byte-order mark is tolerated on read, so a file saved from Notepad — which
adds one by default — is loaded correctly rather than being mistaken for corrupt.

```json
{
  "headsetDeviceId": "{0.0.0.00000000}.{52e88adb-13eb-485e-a66b-2642aa15eab6}",
  "speakerDeviceId": "{0.0.0.00000000}.{f80bc369-90b1-49b8-856c-cac949a23c8f}",
  "setAllRoles": true,
  "soundVolumeViewPath": "",
  "language": "auto",
  "hidMatch": "VID_413C&PID_A529&MI_05&COL02",
  "signatureOnHex": "08 c0 09 03 00 01 cc 0f",
  "signatureOffHex": "08 c0 09 03 00 01 dd 1e",
  "signatureLength": 8,
  "cooldownMs": 1500,
  "reopenDelayMs": 2000,
  "verifyChecksum": false,
  "logUnknownReports": false,
  "startMonitoringOnLaunch": true
}
```

| Field | Type | Default | Meaning |
| --- | --- | --- | --- |
| `headsetDeviceId` | string | `""` | Endpoint ID to switch to when the headset powers on |
| `speakerDeviceId` | string | `""` | Endpoint ID to switch to when it powers off |
| `setAllRoles` | bool | `true` | `true` sets Console/Multimedia/Communications together (as the original script did); `false` sets only Multimedia, which is what "Set as Default Device" does |
| `soundVolumeViewPath` | string | `""` | Optional full path to `SoundVolumeView.exe`, used only as a fallback when the native COM call fails |
| `language` | string | `"auto"` | Interface language: `auto`, `en` or `zh`. An unrecognised value falls back to `auto` |
| `hidMatch` | string | `VID_413C&PID_A529&MI_05&COL02` | Substring of the HID device instance ID |
| `signatureOnHex` | string | `08 c0 09 03 00 01 cc 0f` | Power-on signature, hexadecimal |
| `signatureOffHex` | string | `08 c0 09 03 00 01 dd 1e` | Power-off signature, hexadecimal |
| `signatureLength` | int | `8` | Number of bytes taking part in matching |
| `cooldownMs` | int | `1500` | Cooldown; `0` means "use the default", maximum 60000 |
| `reopenDelayMs` | int | `2000` | Reopen delay; `0` means "use the default", range 100–60000 |
| `verifyChecksum` | bool | `false` | Whether to verify the trailing XOR checksum |
| `logUnknownReports` | bool | `false` | Whether to log unmatched reports |
| `startMonitoringOnLaunch` | bool | `true` | Whether to start monitoring at launch |

`setAllRoles` and `startMonitoringOnLaunch` are nullable internally: a **missing
key** (which takes the default `true`) is distinguishable from an explicit
`false`, so setting either to `false` by hand is not overridden by the default.

Out-of-range or malformed values are corrected on load: an empty match string
takes the default, an unparseable or empty signature takes the default, and a
`signatureLength` longer than the signature itself is clamped into range.

---

## File locations

Everything lives under `%APPDATA%\USBDongle_AudioSwitch\`, **not** beside the
executable — the program writes itself into the HKCU Run key, and would have no
write permission to its own directory if installed under `Program Files`.

| File | Meaning |
| --- | --- |
| `config.json` | Configuration |
| `config.json.tmp` | Temporary file during a save (normally momentary) |
| `config.json.bad` | Backup taken when the config fails to parse |
| `audio_switch.log` | Run log, UTF-8 |
| `audio_switch.log.old` | Rotated archive, created when the log reaches 5 MB (overwrites the previous archive) |

The "Config folder" button opens this directory directly.

---

## Starting with Windows

Ticking "Start with Windows (HKCU Run)" writes:

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
    USBDongle_AudioSwitch = "<full path to the exe>" --tray
```

Using **HKCU rather than HKLM or Task Scheduler** is deliberate: it needs no
administrator rights, and the audio endpoints being switched are **per-user**
settings, so a machine-wide entry would be the wrong scope.

If the program has been moved, the entry points at the old path. On start-up the
program notices and logs a hint — **re-tick the checkbox** to repair it.

---

## HID signatures and the checksum

The receiver sends roughly 62-byte input reports on an interrupt endpoint. The
**first 8 bytes** carry the state change; the rest are zero padding.

| Event | Report pattern |
| --- | --- |
| Power off | A single report |
| Power on | A burst of 8 reports, of which the power-on signature is the first |

### Matching rule

The first `signatureLength` bytes of the report are compared **byte-for-byte** —
no offset, no wildcards.

### XOR checksum

Both built-in signatures satisfy the same relation: **the last byte equals the XOR
of the preceding seven.**

```
on:  08 ^ c0 ^ 09 ^ 03 ^ 00 ^ 01 ^ cc = 0f   →  08 c0 09 03 00 01 cc 0f   ✓
off: 08 ^ c0 ^ 09 ^ 03 ^ 00 ^ 01 ^ dd = 1e   →  08 c0 09 03 00 01 dd 1e   ✓
```

Note that the two signatures differ in their **7th byte** (`cc` versus `dd`), and
the final byte is simply recomputed to follow — each is self-consistent rather
than one being a typo for the other (the live indicator in the window shows
"checksum ✓" for both).

With "Verify trailing XOR checksum" enabled, the check is that the **report's**
final byte equals the XOR of the bytes before it. That rejects reports whose first
7 bytes are right but whose last byte was corrupted in transit. Enabling it
requires `signatureLength >= 2`.

> **Restart monitoring after changing the config**: settings such as
> `verifyChecksum` are read into locals when the monitor thread starts, so a
> change only takes effect after stopping and starting monitoring (clicking
> "Start monitoring" does that cycle for you).

---

## Troubleshooting

### The HID device is never found / the log keeps saying the device is unavailable

1. Check the receiver is plugged in and visible in Device Manager.
2. Click **"List HID devices"**, find your receiver, select it and click "Use as
   match string". The program extracts the `VID/PID/MI/COL` part for you — far
   more reliable than copying an instance ID out of Device Manager by hand.
3. Do **not** include the trailing `9&2658857&0&0001` segment in the match string;
   that is the USB port number and changes when you move the dongle.

The device path is re-resolved before every open, so unplugging, replugging or
moving to another USB port needs no config change. After a read failure the
program waits the reopen delay and retries, indefinitely.

### Power on/off is not detected

1. Tick **"Log unmatched reports"**, then power the headset on and off once and
   look at the bytes actually received.
2. Put the report prefix from the log into the power-on / power-off signature
   boxes. The checksum state is shown live beside each box to help confirm it.
3. Check that "Signature length" matches the number of bytes you entered —
   entering more than the signature contains is a validation error.

### A signal is detected but the device does not switch

1. Read the specific error in the log. The common one is **"no target device is
   configured"** — both drop-downs need a selection before you click "Start
   monitoring".
2. If it says **"the current default device is already the target"**, the live
   default really is already the target; that is the normal skip (for example if
   you just switched manually).
3. If it reports **"IPolicyConfig switch failed"**, the native COM call is being
   blocked. Download [SoundVolumeView](https://www.nirsoft.net/utils/sound_volume_view.html)
   and put its full path in `soundVolumeViewPath` in `config.json` as a fallback.

### Only the multimedia role should change

Set `setAllRoles` to `false` in `config.json`. The default, `true`, sets all three
roles, matching the original script.

### The build cannot find the targeting pack

That is expected — `build.ps1` falls back to the runtime assemblies and prints
`targeting pack: not installed, using the runtime assemblies instead`. Nothing
else is needed. To see genuine environment problems, add `-NoFrameworkPathOverride`.

### How do I really quit it

Closing the window only minimises to the tray. To exit, use **tray icon
right-click → Exit**.

---

## Project layout

```
USBDongle_AudioSwitch/
├─ Program.cs                    Entry point: single instance, activation, last-chance exception handling
├─ AppPaths.cs                   Path constants under %APPDATA%
├─ AppConfig.cs                  Config model + JSON I/O (atomic replace, corrupt-file backup)
├─ Localization.cs               Every user-visible string, in English and Chinese
├─ AppIcons.cs                   Loads the embedded multi-resolution icons, by frame, DPI and theme
├─ SystemTheme.cs                Reads which way the shell is drawing the taskbar
├─ Logger.cs                     Thread-safe log with 5 MB rotation
├─ LogWheelRouter.cs             Routes wheel messages to the log box under the pointer
├─ AutoStartManager.cs           HKCU Run key read/write and staleness detection
├─ Hex.cs                        Hexadecimal parse/format, XOR checksum
├─ MainForm.cs                   Main window logic
├─ MainForm.Designer.cs          Main window layout (pure code, no .resx)
├─ build.ps1                     Build script that needs neither VS nor the SDK
├─ audio_switch.ps1              The original PowerShell script (reference; copied to the output)
├─ Resources/
│  ├─ app.ico                    Window/taskbar/exe icon (9 frames, incl. 256)
│  ├─ tray-white.ico             Tray icon for a dark taskbar (6 frames)
│  └─ tray-black.ico             Tray icon for a light taskbar (6 frames)
├─ assets/
│  ├─ make_icon.py               Builds the three .ico files
│  ├─ art-black.ico              Black line-art master (not shipped)
│  └─ art-white.ico              White line-art master (not shipped)
├─ Audio/
│  ├─ AudioDeviceInfo.cs         DTO for a drop-down entry
│  ├─ AudioDeviceService.cs      Core Audio enumeration: playback devices, current default
│  └─ AudioSwitcher.cs           Switches the default device: IPolicyConfig, then SoundVolumeView
├─ Interop/
│  ├─ AudioInterop.cs            COM declarations for Core Audio / IPolicyConfig
│  ├─ ComApartment.cs            RAII wrapper for COM initialisation (MTA)
│  ├─ SetupApi.cs                SetupAPI P/Invoke (enumerating HID interfaces)
│  └─ Win32.cs                   kernel32/user32 P/Invoke, error-code descriptions, DPI query
└─ Monitoring/
   ├─ HidDeviceLocator.cs        Match string → device path; enumerates HID instance IDs
   ├─ HidMonitor.cs              HID read loop: open, block, match, reconnect
   ├─ HidSignal.cs               PowerOn / PowerOff
   └─ MonitorController.cs       Wires HID signals to the audio switch, exposes events to the UI
```

### Implementation notes

- **No WMI**: the original script used `Get-PnpDevice` (WMI/CIM), which is slow to
  start and depends on the PowerShell environment. This project enumerates with
  SetupAPI directly and has no `System.Management` dependency.
- **Overlapped I/O rather than synchronous reads**: a synchronous `ReadFile` may
  never return when the device is unplugged, which would hang the stop path.
  `HidMonitor` uses `FILE_FLAG_OVERLAPPED` plus `CancelIoEx`, so `Stop()` reaps the
  thread within 5 seconds.
- **Buffers pinned via `IntPtr`**: the read/write buffers in `Win32.cs` are declared
  as `IntPtr` rather than `byte[]`, together with `GCHandle.Alloc(..., Pinned)`, so
  the marshaller cannot move the buffer while an overlapped operation is in flight.
- **Live comparison before switching**: the "current default device" is re-read
  before every switch rather than cached — the user may have changed it by hand,
  and a cache would wrongly skip a switch that is needed.
- **Every C# comment is in English.** The PowerShell build scripts and the WiX
  installer definition still carry Chinese comments and console output.

### When changing the interface

`MainForm.Designer.cs` is hand-written and follows two WinForms traps that have
already bitten once each — read those comments before editing:

- **`AutoScaleMode` is `None`, not `Font`.** `AutoScaleDimensions` says `(7,17)`
  (the measurement for the default 8.25pt font) while the form actually uses
  `YaHei UI 9pt`. That mismatch makes WinForms recompute `ClientSize` at start-up
  (820×504 rendered as 957×714) without rescaling the children's anchor margins.
- **A container's `Size` must be set before its children are added.** WinForms
  records each anchored child's margins against the parent's size *at the moment
  it is added*, and a brand new `GroupBox` / `Panel` / `TableLayoutPanel` is only
  200×100 — so coordinates written for the final size produce **negative** right
  margins, stretching the drop-downs to 1124px and pushing the buttons right off
  the window. The four "seed size" assignments in `_root`, `_audioGroup`,
  `_hidGroup` and `_actionPanel` exist for exactly this reason.
- **No text is set in the designer.** Every caption comes from
  `MainForm.ApplyLocalization()`, which is what allows the language to be switched
  in place. Hard-coding a `.Text` in the designer would leave that caption stuck in
  one language.
- **Widths must fit the English string, which is the longer of the two.** English
  captions run up to 40% wider than the Chinese ones; labels are `AutoSize` and
  will overlap the control to their right if the budget is too small.

---

## Credits

Inspired by [Meladon90/Audio-Switch](https://github.com/Meladon90/Audio-Switch).

# ===== CONFIGURATION =====
# "Headphones" (AWPRO H Wireless Game) - target when the headset powers ON
$HEADSET_ID = "{0.0.0.00000000}.{52e88adb-13eb-485e-a66b-2642aa15eab6}"
# "MPG 271Q X50" (NVIDIA High Definition Audio) - target when the headset powers OFF
$SPEAKER_ID = "{0.0.0.00000000}.{f80bc369-90b1-49b8-856c-cac949a23c8f}"

$SVV_PATH = "C:\Users\a7578\scoop\apps\soundvolumeview\current\SoundVolumeView.exe"
# SoundVolumeView roles to set on switch: 0=Console, 1=Multimedia, 2=Communications.
# Use @(1) for the original script's behaviour (Multimedia only).
$SVV_ROLES = @(0, 1, 2)

# HID collection that carries the receiver's state reports. This is the
# vendor-defined collection (COL02) of interface MI_05 on the AWPRO dongle.
$HID_MATCH = "VID_413C&PID_A529&MI_05&COL02"

$LOG_PATH = "$PSScriptRoot\audio_switch.log"
$MAX_LOG_SIZE_MB = 5
$COOLDOWN_MS = 1500           # Ignore repeat reports within this window
$REOPEN_DELAY_MS = 2000       # Wait before re-opening after a device error
$LOG_UNKNOWN_REPORTS = $false # Log reports that match no known signature

# ===== REPORT SIGNATURES =====
# The receiver sends 62-byte input reports on an interrupt endpoint. The first
# 8 bytes carry the state change; the rest is zero padding. The 8th byte is an
# XOR checksum over the preceding seven, so a match also validates integrity.
#
#   power OFF -> a single report
#   power ON  -> a burst of 8 reports, this signature arriving first
#
$SIG_HEADSET_OFF = [byte[]](0x08, 0xc0, 0x09, 0x03, 0x00, 0x01, 0xdd, 0x1e)
$SIG_HEADSET_ON  = [byte[]](0x08, 0xc0, 0x09, 0x03, 0x00, 0x01, 0xcc, 0x0f)
$SIG_LENGTH = 8

# CreateFileW access flags. Written in decimal because PowerShell parses the
# hex literal 0xC0000000 as a *negative* Int32, whose [uint32] cast throws.
$GENERIC_READ_WRITE = [uint32]3221225472   # 0xC0000000
$FILE_SHARE_RW      = [uint32]3            # 0x00000003
$OPEN_EXISTING      = [uint32]3            # 0x00000003
$INVALID_HANDLE     = [IntPtr](-1)

$currentDevice = $null
$cooldownUntil = Get-Date '1/1/2000'

# ===== LOGGING =====
function Write-Log($message) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $line = "$timestamp - $message"
    Write-Host $line

    if (Test-Path $LOG_PATH) {
        $sizeMB = (Get-Item $LOG_PATH).Length / 1MB
        if ($sizeMB -ge $MAX_LOG_SIZE_MB) {
            Rename-Item $LOG_PATH "$LOG_PATH.old" -Force
        }
    }
    Add-Content -Path $LOG_PATH -Value $line
}

# ===== AUDIO SWITCH =====
function Set-AudioDevice($targetDevice) {
    if ($currentDevice -eq $targetDevice) { return }
    Write-Log "Switching audio to device ID $targetDevice"
    foreach ($role in $SVV_ROLES) {
        & $SVV_PATH /SetDefault "$targetDevice" $role
    }
    $script:currentDevice = $targetDevice
}

# ===== HID PLUMBING =====
if (-not ('Native.Hid' -as [type])) {
    Add-Type -Namespace Native -Name Hid -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError=true, CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
public static extern System.IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
    System.IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, System.IntPtr hTemplateFile);

[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError=true)]
public static extern bool ReadFile(System.IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead,
    out uint lpNumberOfBytesRead, System.IntPtr lpOverlapped);

[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError=true)]
public static extern bool CloseHandle(System.IntPtr hObject);
'@
}

# The "9&2658857&0&0001" suffix in an instance ID encodes the USB port, so the
# device path is resolved at runtime rather than hard-coded - it changes if the
# dongle is moved to another port.
function Get-HidDevicePath {
    $dev = Get-PnpDevice -PresentOnly -Class HIDClass -ErrorAction SilentlyContinue |
           Where-Object { $_.InstanceId -like "*$HID_MATCH*" } |
           Select-Object -First 1
    if (-not $dev) { return $null }
    return "\\?\" + $dev.InstanceId.Replace('\', '#').ToLower() +
           "#{4d1e55b2-f16f-11cf-88cb-001111000030}"
}

function Open-HidDevice {
    $path = Get-HidDevicePath
    if (-not $path) { return [IntPtr]::Zero }

    try {
        $handle = [Native.Hid]::CreateFileW(
            $path,
            $GENERIC_READ_WRITE,
            $FILE_SHARE_RW,
            [IntPtr]::Zero,
            $OPEN_EXISTING,
            [uint32]0,   # synchronous - ReadFile blocks until a report arrives
            [IntPtr]::Zero)
    }
    catch {
        Write-Log "CreateFileW failed for $path : $($_.Exception.Message)"
        return [IntPtr]::Zero
    }

    # Compare via ToInt64: a failed call leaves $handle $null, and
    # $null -eq [IntPtr]::Zero is False, so an -eq test would let null through.
    if ($null -eq $handle) { return [IntPtr]::Zero }
    $raw = $handle.ToInt64()
    if ($raw -eq 0 -or $raw -eq -1) { return [IntPtr]::Zero }

    $script:hidPath = $path
    return $handle
}

function Test-Signature($buffer, $length, $signature) {
    if ($length -lt $SIG_LENGTH) { return $false }
    for ($i = 0; $i -lt $SIG_LENGTH; $i++) {
        if ($buffer[$i] -ne $signature[$i]) { return $false }
    }
    return $true
}

function Format-Bytes($buffer, $length) {
    $n = [Math]::Min($length, 16)
    return (($buffer[0..($n - 1)] | ForEach-Object { $_.ToString('x2') }) -join ' ')
}

# ===== MAIN LOOP =====
Write-Log "Monitoring AWPRO receiver HID reports ($HID_MATCH)..."

while ($true) {
    $handle = Open-HidDevice
    if ($null -eq $handle -or $handle.ToInt64() -eq 0) {
        Write-Log "HID device unavailable ($HID_MATCH) - retrying in $REOPEN_DELAY_MS ms"
        Start-Sleep -Milliseconds $REOPEN_DELAY_MS
        continue
    }

    Write-Log "HID device opened: $hidPath"

    $buffer = New-Object byte[] 256
    $read = [uint32]0

    while ($true) {
        $ok = [Native.Hid]::ReadFile($handle, $buffer, 256, [ref]$read, [IntPtr]::Zero)
        if (-not $ok) {
            $err = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
            Write-Log "Read failed (error $err) - device removed or reset, re-opening"
            break
        }
        if ($read -lt $SIG_LENGTH) { continue }

        $now = Get-Date
        if ($now -lt $cooldownUntil) { continue }

        if (Test-Signature $buffer $read $SIG_HEADSET_OFF) {
            Write-Log "Detected HEADSET OFF ($(Format-Bytes $buffer $read))"
            Set-AudioDevice $SPEAKER_ID
            $cooldownUntil = $now.AddMilliseconds($COOLDOWN_MS)
        }
        elseif (Test-Signature $buffer $read $SIG_HEADSET_ON) {
            Write-Log "Detected HEADSET ON ($(Format-Bytes $buffer $read))"
            Set-AudioDevice $HEADSET_ID
            $cooldownUntil = $now.AddMilliseconds($COOLDOWN_MS)
        }
        elseif ($LOG_UNKNOWN_REPORTS) {
            Write-Log "Unmatched report (len $read): $(Format-Bytes $buffer $read)"
        }
    }

    [Native.Hid]::CloseHandle($handle) | Out-Null
    Start-Sleep -Milliseconds $REOPEN_DELAY_MS
}

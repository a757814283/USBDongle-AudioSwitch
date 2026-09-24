using System;
using System.Runtime.InteropServices;

namespace USBDongle_AudioSwitch.Interop
{
    /// <summary>
    /// kernel32 entry points used by the HID reader.
    ///
    /// The buffers are declared as IntPtr rather than byte[] because the monitor
    /// issues *overlapped* reads: once ReadFile returns ERROR_IO_PENDING the
    /// buffer and the OVERLAPPED block must stay pinned and untouched until the
    /// I/O completes. The default marshaller only pins for the duration of the
    /// call, which would let the GC move the buffer out from under the driver.
    /// </summary>
    internal static class Win32
    {
        // CreateFileW access / sharing / disposition flags.
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint OPEN_EXISTING = 0x00000003;

        // Requests asynchronous completion so a pending read can be cancelled
        // instead of pinning a thread until the dongle sends something.
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        public const int ERROR_ACCESS_DENIED = 5;
        public const int ERROR_INVALID_HANDLE = 6;
        public const int ERROR_OPERATION_ABORTED = 995;
        public const int ERROR_IO_PENDING = 997;
        public const int ERROR_DEVICE_NOT_CONNECTED = 1167;
        public const int ERROR_NO_SUCH_DEVICE = 433;

        public const uint WAIT_OBJECT_0 = 0x00000000;
        public const uint WAIT_TIMEOUT = 0x00000102;
        public const uint WAIT_FAILED = 0xFFFFFFFF;

        /// <summary>Returned by CreateFileW on failure.</summary>
        public static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern IntPtr CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool ReadFile(
            IntPtr hFile,
            IntPtr lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool GetOverlappedResult(
            IntPtr hFile,
            IntPtr lpOverlapped,
            out uint lpNumberOfBytesTransferred,
            bool bWait);

        /// <summary>
        /// Unblocks a pending overlapped ReadFile. Passing a null OVERLAPPED
        /// cancels every outstanding request on the handle.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool CancelIoEx(IntPtr hFile, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern IntPtr CreateEventW(
            IntPtr lpEventAttributes,
            bool bManualReset,
            bool bInitialState,
            string lpName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool ResetEvent(IntPtr hEvent);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr GetModuleHandleW(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public const int SW_RESTORE = 9;

        /// <summary>Window under a screen point, used to route wheel messages.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // Scrollbar access for the read-only log. A TextBox offers no managed
        // way to read or restore its scroll offset, which is needed to stop
        // AppendText from dragging the view back to the bottom.
        public const int SB_VERT = 1;
        public const int EM_LINESCROLL = 0x00B6;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetScrollPos(IntPtr hWnd, int nBar);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetScrollRange(IntPtr hWnd, int nBar, out int lpMinPos, out int lpMaxPos);

        /// <summary>
        /// Broadcast to every top-level window when a system-wide setting has
        /// changed. <c>lParam</c> names the area that moved, or is null when the
        /// sender did not say.
        /// </summary>
        public const int WM_SETTINGCHANGE = 0x001A;

        /// <summary>
        /// The area named by a WM_SETTINGCHANGE, or null when it names none.
        ///
        /// lParam is a pointer to a string that stays valid only for the duration
        /// of the send, so it has to be read from inside the message handler.
        /// Only ever called with a WM_SETTINGCHANGE: for any other message the
        /// pointer means something else and dereferencing it would be a crash.
        /// </summary>
        public static string SettingChangeArea(IntPtr lParam)
        {
            return lParam == IntPtr.Zero ? null : Marshal.PtrToStringAuto(lParam);
        }

        // ---- DPI, for sizing the tray icon ---------------------------------

        /// <summary>Per-monitor DPI awareness, as a thread-scoped context.</summary>
        public static readonly IntPtr DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = new IntPtr(-2);

        /// <summary>
        /// Temporarily changes the calling thread's DPI awareness.
        ///
        /// This process is DPI-*unaware* - deliberately, so the hand-tuned window
        /// layout is not rescaled - and an unaware thread is told the DPI is 96
        /// whatever the display is really set to. Switching the thread's context
        /// for the duration of one call is the supported way to read the true
        /// value without changing how the process draws. Returns the previous
        /// context, to be passed back when the measurement is done.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        /// <summary>System DPI, meaningful only while the thread is DPI-aware.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetDpiForSystem();

        /// <summary>Human-readable form of the last Win32 error, for the log.</summary>
        public static string DescribeLastError()
        {
            int code = Marshal.GetLastWin32Error();
            return "error " + code + " (" + new System.ComponentModel.Win32Exception(code).Message + ")";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OVERLAPPED
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        public uint Offset;
        public uint OffsetHigh;
        public IntPtr hEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}

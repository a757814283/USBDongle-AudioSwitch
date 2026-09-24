using System;
using System.Runtime.InteropServices;
using System.Threading;
using USBDongle_AudioSwitch.Interop;

namespace USBDongle_AudioSwitch.Monitoring
{
    /// <summary>
    /// Owns the HID read loop: locate the receiver, open it, block on input
    /// reports until one matches a power signature, then hand off to the audio
    /// switcher. On any device error the handle is closed and the whole cycle
    /// restarts after a delay, so unplugging and re-plugging the dongle - or
    /// moving it to another USB port - recovers on its own.
    ///
    /// Reads are issued as *overlapped* I/O and waited on with a timeout. The
    /// payload still arrives by blocking on the endpoint, but the wait is
    /// interruptible, which is what lets "stop" take effect immediately instead
    /// of hanging until the headset is next powered on.
    /// </summary>
    internal sealed class HidMonitor : IDisposable
    {
        private const uint WaitTickMs = 500;
        private const uint CancelReapTimeoutMs = 2000;

        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);

        private Thread _thread;
        private volatile bool _stopRequested;

        private IntPtr _handle = IntPtr.Zero;
        private IntPtr _readEvent = IntPtr.Zero;
        private IntPtr _overlapped = IntPtr.Zero;
        private byte[] _buffer;

        /// <summary>Informational text for the log pane. Fires on the monitor thread.</summary>
        public event Action<string> Message;

        /// <summary>Raised once each time a device path is successfully opened.</summary>
        public event Action<string> DeviceOpened;

        /// <summary>Raised on the monitor thread when a report matches a signature.</summary>
        public event Action<HidSignal, string> SignalDetected;

        public bool IsRunning
        {
            get { return _thread != null; }
        }

        public void Start(AppConfig config)
        {
            if (_thread != null)
            {
                return;
            }

            _stopRequested = false;
            _stopEvent.Reset();

            AppConfig snapshot = config;
            _thread = new Thread(() => Run(snapshot));
            _thread.IsBackground = true;
            _thread.Name = "HID monitor";
            _thread.Start();
        }

        public void Stop()
        {
            Thread thread = _thread;
            if (thread == null)
            {
                return;
            }

            _stopRequested = true;
            _stopEvent.Set();

            // Unblock a read that is already pending so the loop can unwind.
            IntPtr handle = _handle;
            if (handle != IntPtr.Zero)
            {
                Win32.CancelIoEx(handle, IntPtr.Zero);
            }

            if (!thread.Join(5000))
            {
                Logger.Warn(Loc.T(Str.LogMonitorThreadStuck));
            }

            _thread = null;
        }

        public void Dispose()
        {
            Stop();
            CloseDevice();
            _stopEvent.Close();
        }

        // ---- monitor thread ------------------------------------------------

        private void Run(AppConfig config)
        {
            byte[] onSignature = config.SignatureOnBytes;
            byte[] offSignature = config.SignatureOffBytes;
            int signatureLength = config.SignatureLength;
            bool verifyChecksum = config.VerifyChecksum;

            while (!_stopRequested)
            {
                string path = HidDeviceLocator.FindDevicePath(config.HidMatch);
                if (string.IsNullOrEmpty(path))
                {
                    Report(Loc.T(Str.LogHidNotFound, config.HidMatch, config.ReopenDelayMs));
                    if (WaitForStop(config.ReopenDelayMs))
                    {
                        break;
                    }
                    continue;
                }

                if (!OpenDevice(path))
                {
                    if (WaitForStop(config.ReopenDelayMs))
                    {
                        break;
                    }
                    continue;
                }

                Action<string> opened = DeviceOpened;
                if (opened != null)
                {
                    opened(path);
                }
                Report(Loc.T(Str.LogHidOpened, path));

                try
                {
                    ReadLoop(config, onSignature, offSignature, signatureLength, verifyChecksum);
                }
                catch (Exception ex)
                {
                    Report(Loc.T(Str.LogReadLoopAborted,
                        ex.GetType().Name + ": " + ex.Message));
                }
                finally
                {
                    CloseDevice();
                }

                if (_stopRequested)
                {
                    break;
                }

                Report(Loc.T(Str.LogHidReadEnded, config.ReopenDelayMs));
                if (WaitForStop(config.ReopenDelayMs))
                {
                    break;
                }
            }

            Report(Loc.T(Str.LogMonitorStopped));
        }

        private void ReadLoop(AppConfig config, byte[] onSignature, byte[] offSignature,
            int signatureLength, bool verifyChecksum)
        {
            GCHandle pinned = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            try
            {
                IntPtr bufferPointer = pinned.AddrOfPinnedObject();
                long cooldownUntilTicks = 0;

                while (!_stopRequested)
                {
                    uint bytesRead;
                    if (!ReadReport(bufferPointer, out bytesRead))
                    {
                        return;
                    }

                    if (_stopRequested)
                    {
                        return;
                    }

                    if (bytesRead < signatureLength)
                    {
                        continue;
                    }

                    // Cooldown is checked before matching, exactly as in the
                    // original script: powering the headset on emits a burst of
                    // reports and only the first should cause a switch.
                    if (DateTime.UtcNow.Ticks < cooldownUntilTicks)
                    {
                        continue;
                    }

                    if (Matches(_buffer, bytesRead, offSignature, signatureLength, verifyChecksum))
                    {
                        Report(Loc.T(Str.LogSignalOff, Hex.Format(_buffer, (int)bytesRead, 16)));
                        RaiseSignal(HidSignal.PowerOff, bytesRead);
                        cooldownUntilTicks = DateTime.UtcNow.AddMilliseconds(config.CooldownMs).Ticks;
                    }
                    else if (Matches(_buffer, bytesRead, onSignature, signatureLength, verifyChecksum))
                    {
                        Report(Loc.T(Str.LogSignalOn, Hex.Format(_buffer, (int)bytesRead, 16)));
                        RaiseSignal(HidSignal.PowerOn, bytesRead);
                        cooldownUntilTicks = DateTime.UtcNow.AddMilliseconds(config.CooldownMs).Ticks;
                    }
                    else if (config.LogUnknownReports)
                    {
                        Report(Loc.T(Str.LogUnmatchedReport, bytesRead,
                            Hex.Format(_buffer, (int)bytesRead, 16)));
                    }
                }
            }
            finally
            {
                pinned.Free();
            }
        }

        /// <summary>
        /// Issues one overlapped read and waits for it. Returns false when the
        /// read failed or the loop was asked to stop - in both cases the caller
        /// must abandon the handle and reopen.
        /// </summary>
        private bool ReadReport(IntPtr bufferPointer, out uint bytesRead)
        {
            bytesRead = 0;

            Win32.ResetEvent(_readEvent);

            OVERLAPPED overlapped = new OVERLAPPED();
            overlapped.hEvent = _readEvent;
            Marshal.StructureToPtr(overlapped, _overlapped, false);

            uint immediateRead;
            bool completed = Win32.ReadFile(_handle, bufferPointer, (uint)_buffer.Length,
                out immediateRead, _overlapped);

            if (!completed)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != Win32.ERROR_IO_PENDING)
                {
                    Report(Loc.T(Str.LogReadFileFailed, DescribeError(error)));
                    return false;
                }

                if (!WaitForCompletion(out bytesRead))
                {
                    return false;
                }
            }
            else
            {
                bytesRead = immediateRead;
            }

            return true;
        }

        /// <summary>
        /// Waits in short slices so a pending request can notice a stop request
        /// without waiting for the dongle to send something.
        /// </summary>
        private bool WaitForCompletion(out uint bytesRead)
        {
            bytesRead = 0;

            while (true)
            {
                uint wait = Win32.WaitForSingleObject(_readEvent, WaitTickMs);

                if (wait == Win32.WAIT_OBJECT_0)
                {
                    break;
                }

                if (wait == Win32.WAIT_TIMEOUT)
                {
                    if (!_stopRequested)
                    {
                        continue;
                    }

                    // Cancel, then reap the completion so the pinned buffer is
                    // guaranteed to be idle before the caller frees it.
                    Win32.CancelIoEx(_handle, _overlapped);
                    Win32.WaitForSingleObject(_readEvent, CancelReapTimeoutMs);

                    uint discarded;
                    Win32.GetOverlappedResult(_handle, _overlapped, out discarded, false);
                    return false;
                }

                Report(Loc.T(Str.LogWaitFailed, Win32.DescribeLastError()));
                return false;
            }

            uint transferred;
            if (!Win32.GetOverlappedResult(_handle, _overlapped, out transferred, false))
            {
                int error = Marshal.GetLastWin32Error();

                // A cancelled request is the normal way to leave on stop.
                if (error == Win32.ERROR_OPERATION_ABORTED)
                {
                    return false;
                }

                Report(Loc.T(Str.LogReadFailed, DescribeError(error)));
                return false;
            }

            bytesRead = transferred;
            return true;
        }

        private static bool Matches(byte[] buffer, uint length, byte[] signature,
            int signatureLength, bool verifyChecksum)
        {
            if (buffer == null || signature == null)
            {
                return false;
            }
            if (signatureLength <= 0 || signatureLength > signature.Length || length < signatureLength)
            {
                return false;
            }

            for (int i = 0; i < signatureLength; i++)
            {
                if (buffer[i] != signature[i])
                {
                    return false;
                }
            }

            // Optional integrity check: the trailing byte is the XOR of the
            // preceding ones, so a corrupt-but-equal prefix is rejected.
            if (verifyChecksum && signatureLength >= 2)
            {
                byte expected = Hex.XorChecksum(buffer, signatureLength - 1);
                if (buffer[signatureLength - 1] != expected)
                {
                    return false;
                }
            }

            return true;
        }

        private void RaiseSignal(HidSignal signal, uint bytesRead)
        {
            Action<HidSignal, string> handler = SignalDetected;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(signal, Hex.Format(_buffer, (int)bytesRead, 16));
            }
            catch (Exception ex)
            {
                Logger.Error(Loc.T(Str.LogSignalHandlingError), ex);
            }
        }

        // ---- device lifetime ------------------------------------------------

        private bool OpenDevice(string path)
        {
            _handle = Win32.CreateFileW(
                path,
                Win32.GENERIC_READ | Win32.GENERIC_WRITE,
                Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32.OPEN_EXISTING,
                Win32.FILE_FLAG_OVERLAPPED,
                IntPtr.Zero);

            if (_handle == Win32.InvalidHandleValue || _handle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                _handle = IntPtr.Zero;
                Report(Loc.T(Str.LogOpenHidFailed, DescribeError(error), path));
                return false;
            }

            _readEvent = Win32.CreateEventW(IntPtr.Zero, true, false, null);
            if (_readEvent == IntPtr.Zero)
            {
                Report(Loc.T(Str.LogCreateOverlappedEventFailed, Win32.DescribeLastError()));
                CloseDevice();
                return false;
            }

            _overlapped = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(OVERLAPPED)));
            _buffer = new byte[AppConfig.DefaultReportBufferSize];
            return true;
        }

        private void CloseDevice()
        {
            if (_handle != IntPtr.Zero)
            {
                Win32.CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
            if (_readEvent != IntPtr.Zero)
            {
                Win32.CloseHandle(_readEvent);
                _readEvent = IntPtr.Zero;
            }
            if (_overlapped != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_overlapped);
                _overlapped = IntPtr.Zero;
            }
            _buffer = null;
        }

        /// <summary>Waits up to <paramref name="milliseconds"/>. True if the loop should stop.</summary>
        private bool WaitForStop(int milliseconds)
        {
            if (_stopRequested)
            {
                return true;
            }

            _stopEvent.WaitOne(milliseconds);
            return _stopRequested;
        }

        private void Report(string message)
        {
            Action<string> handler = Message;
            if (handler != null)
            {
                try
                {
                    handler(message);
                }
                catch (Exception)
                {
                }
            }
        }

        private static string DescribeError(int code)
        {
            return "error " + code + " (" + new System.ComponentModel.Win32Exception(code).Message + ")";
        }
    }
}

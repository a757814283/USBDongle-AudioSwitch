using System;
using System.Windows.Forms;
using USBDongle_AudioSwitch.Interop;

namespace USBDongle_AudioSwitch
{
    /// <summary>
    /// Lets the log be scrolled with the wheel on hover, without first clicking
    /// into it.
    ///
    /// Windows posts WM_MOUSEWHEEL to whichever window holds focus rather than
    /// the one under the pointer. The log is read-only, so it rarely has focus,
    /// and a wheel over it would otherwise do nothing. This filter catches the
    /// message on its way past and re-addresses it to the log when the pointer
    /// is genuinely over it.
    ///
    /// Focus is deliberately not taken: stealing it would interrupt whatever
    /// field the user is typing in as soon as the pointer crossed the log.
    /// </summary>
    internal sealed class LogWheelRouter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        private readonly Control _log;

        public LogWheelRouter(Control log)
        {
            _log = log;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL || _log == null || _log.IsDisposed
                || !_log.IsHandleCreated)
            {
                return false;
            }

            // lParam packs the pointer's *screen* position as two signed
            // 16-bit values, low word first.
            long packed = m.LParam.ToInt64();
            int screenX = unchecked((short)(packed & 0xFFFF));
            int screenY = unchecked((short)((packed >> 16) & 0xFFFF));

            IntPtr under = Win32.WindowFromPoint(new POINT(screenX, screenY));
            if (under == IntPtr.Zero || Control.FromHandle(under) != _log)
            {
                return false;
            }

            // Deliver it to the log directly; the focused control would
            // otherwise swallow it.
            try
            {
                Win32.SendMessageW(_log.Handle, WM_MOUSEWHEEL, m.WParam, m.LParam);
            }
            catch (Exception)
            {
                // Never let a wheel event take down the message pump.
                return false;
            }

            return true;
        }
    }
}

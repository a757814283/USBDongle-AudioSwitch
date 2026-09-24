using System;

namespace USBDongle_AudioSwitch.Interop
{
    /// <summary>
    /// Initialises COM for the lifetime of a scope.
    ///
    /// The audio endpoints are reached through COM from the monitor thread, which
    /// the CLR does not initialise on its own. If the thread is already in an
    /// apartment (RPC_E_CHANGED_MODE) the existing state is left alone and
    /// CoUninitialize is deliberately *not* called, since the mismatch means
    /// somebody else owns the apartment.
    /// </summary>
    internal sealed class ComApartment : IDisposable
    {
        private readonly bool _ownsApartment;

        private ComApartment(bool ownsApartment)
        {
            _ownsApartment = ownsApartment;
        }

        public static ComApartment Enter()
        {
            int hr = Ole32.CoInitializeEx(IntPtr.Zero, Ole32.COINIT_MULTITHREADED);

            // S_OK (0) means we just initialised it; S_FALSE (1) means it was
            // already initialised with the same mode and still needs a matching
            // CoUninitialize.
            bool owns = hr == 0 || hr == Ole32.S_FALSE;
            return new ComApartment(owns);
        }

        public void Dispose()
        {
            if (_ownsApartment)
            {
                Ole32.CoUninitialize();
            }
        }
    }
}

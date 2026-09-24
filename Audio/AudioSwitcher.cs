using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using USBDongle_AudioSwitch.Interop;

namespace USBDongle_AudioSwitch.Audio
{
    /// <summary>
    /// Changes the system default playback endpoint.
    ///
    /// The primary path is the native IPolicyConfig COM interface, which is what
    /// SoundVolumeView drives internally - using it directly means no external
    /// executable has to ship with the program. SoundVolumeView remains available
    /// as an opt-in fallback for machines where the COM call is blocked.
    /// </summary>
    internal static class AudioSwitcher
    {
        /// <summary>Roles written when "set all roles" is enabled, matching the original script.</summary>
        private static readonly ERole[] AllRoles =
        {
            ERole.eConsole,
            ERole.eMultimedia,
            ERole.eCommunications
        };

        private static readonly ERole[] MultimediaOnly = { ERole.eMultimedia };

        public static bool TrySetDefault(string endpointId, bool allRoles, string soundVolumeViewPath,
            out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(endpointId))
            {
                error = Loc.T(Str.ErrorNoTargetDeviceId);
                return false;
            }

            ERole[] roles = allRoles ? AllRoles : MultimediaOnly;

            string comError;
            if (TrySetDefaultViaPolicyConfig(endpointId, roles, out comError))
            {
                return true;
            }

            Logger.Warn(Loc.T(Str.LogPolicyConfigFailed, comError));

            if (!string.IsNullOrWhiteSpace(soundVolumeViewPath) && File.Exists(soundVolumeViewPath))
            {
                Logger.Info(Loc.T(Str.LogTryingSoundVolumeView, soundVolumeViewPath));
                string svvError;
                if (TrySetDefaultViaSoundVolumeView(soundVolumeViewPath, endpointId, roles, out svvError))
                {
                    return true;
                }
                error = Loc.T(Str.ErrorBothStrategiesFailed, comError, svvError);
                return false;
            }

            error = Loc.T(Str.ErrorNativeFailedNoFallback, comError);
            return false;
        }

        private static bool TrySetDefaultViaPolicyConfig(string endpointId, ERole[] roles, out string error)
        {
            error = null;

            using (ComApartment.Enter())
            {
                IPolicyConfig policyConfig = null;
                try
                {
                    policyConfig = (IPolicyConfig)new CPolicyConfigClient();

                    foreach (ERole role in roles)
                    {
                        int hr = policyConfig.SetDefaultEndpoint(endpointId, role);
                        if (hr != 0)
                        {
                            error = "SetDefaultEndpoint(" + role + ") HRESULT=0x" + hr.ToString("X8");
                            return false;
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                    return false;
                }
                finally
                {
                    if (policyConfig != null)
                    {
                        try
                        {
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(policyConfig);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
        }

        private static bool TrySetDefaultViaSoundVolumeView(string exePath, string endpointId,
            ERole[] roles, out string error)
        {
            error = null;

            foreach (ERole role in roles)
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(exePath)
                    {
                        Arguments = "/SetDefault \"" + endpointId + "\" " + (int)role,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (Process process = Process.Start(startInfo))
                    {
                        if (process == null)
                        {
                            error = Loc.T(Str.ErrorSoundVolumeViewStartFailed);
                            return false;
                        }

                        // Drain the pipes before waiting, otherwise a chatty child
                        // can fill the buffer and deadlock the wait.
                        process.StandardOutput.ReadToEnd();
                        process.StandardError.ReadToEnd();

                        if (!process.WaitForExit(10000))
                        {
                            error = Loc.T(Str.ErrorSoundVolumeViewTimeout, role);
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                    return false;
                }
            }

            return true;
        }

        public static IList<ERole> GetRoles(bool allRoles)
        {
            return allRoles ? (IList<ERole>)AllRoles : MultimediaOnly;
        }
    }
}

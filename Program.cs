using System;
using System.Threading;
using System.Windows.Forms;

namespace USBDongle_AudioSwitch
{
    internal static class Program
    {
        /// <summary>Command-line switch written into the Run key: start hidden in the tray.</summary>
        public const string TrayArgument = "--tray";

        private const string MutexName = @"Local\USBDongle_AudioSwitch.SingleInstance";
        private const string ActivateEventName = @"Local\USBDongle_AudioSwitch.Activate";

        [STAThread]
        private static void Main(string[] args)
        {
            // Settle on a language before anything can log or show a message.
            // The config file may name a different one, but it cannot be read
            // until the paths exist and it is the config loader itself that
            // wants to log - so the system language covers that window, and
            // MainForm switches to the configured language once it has loaded.
            Loc.Apply(AppLanguage.Auto);

            AppPaths.EnsureConfigDirectory();
            Logger.LogPath = AppPaths.LogFile;

            // The activation event is created before the mutex on purpose. A
            // second instance can only get past the mutex check once the first
            // instance owns it, and by then its event handle is guaranteed to
            // exist - so the "open existing" call below cannot race.
            using (EventWaitHandle activateSignal = new EventWaitHandle(
                       false, EventResetMode.AutoReset, ActivateEventName))
            {
                bool createdNew;
                using (Mutex mutex = new Mutex(false, MutexName, out createdNew))
                {
                    if (!createdNew)
                    {
                        BringExistingInstanceToFront(activateSignal);
                        return;
                    }

                    RunApplication(args, activateSignal);
                }
            }
        }

        private static void RunApplication(string[] args, EventWaitHandle activateSignal)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // A background monitor must not die on an unexpected UI exception.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException +=
                (sender, e) => ReportFatal(Loc.T(Str.FatalUiThreadException), e.Exception);
            AppDomain.CurrentDomain.UnhandledException +=
                (sender, e) => ReportFatal(Loc.T(Str.FatalUnhandledException),
                    e.ExceptionObject as Exception);

            Logger.Info(Loc.T(Str.LogProgramStarted));

            MainForm form = new MainForm(IsTrayLaunch(args));
            StartActivationListener(activateSignal, form);

            Application.Run(form);
            Logger.Info(Loc.T(Str.LogProgramExited));
        }

        private static bool IsTrayLaunch(string[] args)
        {
            if (args == null)
            {
                return false;
            }

            foreach (string arg in args)
            {
                if (string.Equals(arg, TrayArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Waits for a second launch and surfaces the existing window. Runs on a
        /// background thread because the wait outlives any single message loop
        /// iteration.
        /// </summary>
        private static void StartActivationListener(EventWaitHandle activateSignal, MainForm form)
        {
            Thread listener = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        activateSignal.WaitOne();
                        form.BeginInvoke(new Action(form.ShowFromTray));
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (InvalidOperationException)
                    {
                        // Form handle not created yet or already destroyed.
                        return;
                    }
                }
            });

            listener.IsBackground = true;
            listener.Name = "Single-instance listener";
            listener.Start();
        }

        private static void BringExistingInstanceToFront(EventWaitHandle activateSignal)
        {
            try
            {
                activateSignal.Set();
            }
            catch (Exception)
            {
            }
        }

        private static void ReportFatal(string context, Exception ex)
        {
            Logger.Error(context, ex);
        }
    }
}

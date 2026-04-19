using System;
using System.Globalization;
using System.Threading;
using System.Windows;

namespace PCLockScreen
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Catch any unhandled exception and show it so we can diagnose
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                MessageBox.Show(ex.ExceptionObject?.ToString(), "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show(ex.Exception?.ToString(), "Dispatcher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            try
            {
                base.OnStartup(e);

                // Ensure only one instance is running
                bool createdNew;
                var mutex = new System.Threading.Mutex(true, "PCLockScreen_SingleInstance", out createdNew);

                if (!createdNew)
                {
                    MessageBox.Show(Loc.Instance.Strings.AppAlreadyRunning, Loc.Instance.Strings.AppAlreadyRunningTitle,
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }

                // Load persisted language preference (defaults to English)
                var config = new ConfigManager().LoadConfig();
                var lang = string.IsNullOrWhiteSpace(config.Language) ? "en" : config.Language;
                Loc.Instance.SetLanguage(lang);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}

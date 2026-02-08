using System;
using System.Media;
using System.Windows;
using System.Windows.Threading;

namespace PCLockScreen
{
    /// <summary>
    /// Interaction logic for ReminderWindow.xaml
    /// Pop-up window to display reminder notifications
    /// </summary>
    public partial class ReminderWindow : Window
    {
        private DispatcherTimer autoDismissTimer;

        public ReminderWindow(string message)
        {
            InitializeComponent();
            ReminderMessage.Text = message ?? "Reminder";
            
            // Play notification sound
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to play notification sound", ex);
            }
            
            // Auto-dismiss after 30 seconds
            autoDismissTimer = new DispatcherTimer();
            autoDismissTimer.Interval = TimeSpan.FromSeconds(30);
            autoDismissTimer.Tick += (s, e) =>
            {
                autoDismissTimer.Stop();
                Close();
            };
            autoDismissTimer.Start();
        }

        private void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            if (autoDismissTimer != null)
            {
                autoDismissTimer.Stop();
            }
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (autoDismissTimer != null)
            {
                autoDismissTimer.Stop();
            }
            base.OnClosing(e);
        }

        /// <summary>
        /// Show the reminder window on the UI thread
        /// </summary>
        public static void ShowReminder(string message)
        {
            try
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var window = new ReminderWindow(message);
                    window.Show();
                    window.Activate();
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to show reminder window", ex);
            }
        }
    }
}

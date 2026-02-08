using System;
using System.Windows;

namespace PCLockScreen
{
    /// <summary>
    /// Interaction logic for ReminderWindow.xaml
    /// Pop-up window to display reminder notifications
    /// </summary>
    public partial class ReminderWindow : Window
    {
        public ReminderWindow(string message)
        {
            InitializeComponent();
            ReminderMessage.Text = message ?? "Reminder";
        }

        private void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            Close();
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

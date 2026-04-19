using System;
using System.Collections.Generic;
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

        // Track all open reminder windows so we can stack them vertically
        private static readonly List<ReminderWindow> _openWindows = new List<ReminderWindow>();
        private const double StackGap = 10;

        public ReminderWindow(string message, bool persistent = false)
        {
            InitializeComponent();
            ReminderMessage.Text = message ?? "Reminder";

            // Position after layout so ActualWidth/Height are available
            Loaded += (_, __) => PositionWindow();
            
            // Play notification sound
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to play notification sound", ex);
            }
            
            // Auto-dismiss after 30 seconds unless persistent
            if (!persistent)
            {
                autoDismissTimer = new DispatcherTimer();
                autoDismissTimer.Interval = TimeSpan.FromSeconds(30);
                autoDismissTimer.Tick += (s, e) =>
                {
                    autoDismissTimer.Stop();
                    Close();
                };
                autoDismissTimer.Start();
            }
        }

        private void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            if (autoDismissTimer != null)
            {
                autoDismissTimer.Stop();
            }
            Close();
        }

        private void PositionWindow()
        {
            var screen = System.Windows.SystemParameters.WorkArea;
            double centreLeft = screen.Left + (screen.Width - ActualWidth) / 2;

            // Stack below all existing open reminder windows
            double top = screen.Top + 40;
            foreach (var w in _openWindows)
            {
                double bottom = w.Top + w.ActualHeight + StackGap;
                if (bottom > top) top = bottom;
            }

            Left = centreLeft;
            Top  = top;
            _openWindows.Add(this);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (autoDismissTimer != null)
            {
                autoDismissTimer.Stop();
            }
            _openWindows.Remove(this);
            base.OnClosing(e);
        }

        /// <summary>
        /// Show the reminder window on the UI thread
        /// </summary>
        public static void ShowReminder(string message, bool persistent = false)
        {
            try
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var window = new ReminderWindow(message, persistent);
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

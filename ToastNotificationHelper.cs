using System;
using System.Windows;

namespace PCLockScreen
{
    public static class ToastNotificationHelper
    {
        /// <summary>
        /// Shows a custom notification window (more reliable than PowerShell toasts)
        /// </summary>
        private static void ShowNotificationWindow(string title, string message, int autoCloseSeconds = 15, bool isUrgent = false)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var notification = new NotificationWindow(title, message, autoCloseSeconds, isUrgent);
                    notification.Show();
                    Logger.Log($"Notification window shown: {title} - {message}");
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"Error showing notification window: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a warning toast notification (5 minutes before lock)
        /// </summary>
        public static void ShowWarningToast(int minutesRemaining)
        {
            ShowNotificationWindow(
                "⚠️ PC Lock Warning",
                $"Your PC will be locked in {minutesRemaining} minutes.\n\nPlease save your work and wrap up.",
                20,
                false
            );
            
            // Play gentle notification sound
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        /// <summary>
        /// Shows an urgent toast notification (1 minute before lock)
        /// </summary>
        public static void ShowUrgentToast()
        {
            ShowNotificationWindow(
                "⚠️ PC Lock Imminent",
                "Your PC will be locked in 1 minute.\n\nPlease save your work now.",
                30,
                true
            );
            
            // Play gentle but noticeable sound
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        /// <summary>
        /// Shows a success toast notification
        /// </summary>
        public static void ShowSuccessToast(string title, string message)
        {
            ShowNotificationWindow(title, message, 10);
        }

        /// <summary>
        /// Shows a general toast notification
        /// </summary>
        public static void ShowToast(string title, string message)
        {
            ShowNotificationWindow(title, message, 15);
        }
    }
}

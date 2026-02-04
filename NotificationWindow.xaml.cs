using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCLockScreen
{
    public partial class NotificationWindow : Window
    {
        private DispatcherTimer autoCloseTimer;
        
        public NotificationWindow(string title, string message, int autoCloseSeconds = 15, bool isUrgent = false)
        {
            InitializeComponent();
            
            TitleText.Text = title;
            MessageText.Text = message;
            
            // Color-code based on urgency
            if (isUrgent)
            {
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 20, 60)); // Crimson red
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(220, 20, 60));
                MainBorder.BorderThickness = new Thickness(4);
            }
            
            // Auto-close after specified seconds
            autoCloseTimer = new DispatcherTimer();
            autoCloseTimer.Interval = TimeSpan.FromSeconds(autoCloseSeconds);
            autoCloseTimer.Tick += (s, e) =>
            {
                autoCloseTimer.Stop();
                Close();
            };
            autoCloseTimer.Start();
        }
        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position in bottom-right corner
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Bottom - Height - 20;
            
            // Slide in animation
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = workArea.Bottom,
                To = workArea.Bottom - Height - 20,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            BeginAnimation(TopProperty, slideIn);
        }
        
        private void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            autoCloseTimer?.Stop();
            Close();
        }
    }
}

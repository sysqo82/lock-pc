using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PCLockScreen
{
    public partial class ScheduleWindow : Window
    {
        private ConfigManager configManager;

        public ScheduleWindow(ConfigManager configManager)
        {
            InitializeComponent();
            this.configManager = configManager;
            LoadSchedule();
        }

        public void LoadSchedule()
        {
            SchedulePanel.Children.Clear();
            
            var config = configManager.LoadConfig();

            // If no account is configured, don't show any schedule details.
            if (string.IsNullOrWhiteSpace(config.AccountEmail))
            {
                StatusText.Text = "Please log in from the main window to view the lock schedule.";
                return;
            }
            
            if (!config.TimeRestrictionEnabled)
            {
                StatusText.Text = "Time restrictions are currently disabled.";
                
                var noRestrictionBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(15),
                    Margin = new Thickness(10)
                };
                
                var messageText = new TextBlock
                {
                    Text = "✓ PC can be used at any time - no restrictions configured",
                    FontSize = 16,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")),
                    TextWrapping = TextWrapping.Wrap
                };
                
                noRestrictionBorder.Child = messageText;
                SchedulePanel.Children.Add(noRestrictionBorder);
                return;
            }
            
            if (config.TimeBlocks.Count == 0)
            {
                StatusText.Text = "Time restrictions enabled but no blocks configured.";
                
                var noBlocksBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(15),
                    Margin = new Thickness(10)
                };
                
                var messageText = new TextBlock
                {
                    Text = "⚠ Time restrictions are enabled but no time blocks are configured.\nPC is currently unrestricted.",
                    FontSize = 16,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100")),
                    TextWrapping = TextWrapping.Wrap
                };
                
                noBlocksBorder.Child = messageText;
                SchedulePanel.Children.Add(noBlocksBorder);
                return;
            }
            
            StatusText.Text = $"Time restrictions are enabled with {config.TimeBlocks.Count} block(s) configured.";
            
            // Display each time block
            for (int i = 0; i < config.TimeBlocks.Count; i++)
            {
                var block = config.TimeBlocks[i];
                
                var blockBorder = new Border();
                var blockPanel = new StackPanel();
                
                // Block title
                var titleText = new TextBlock
                {
                    Text = $"Lock Period #{i + 1}",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F")),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                blockPanel.Children.Add(titleText);
                
                // Time range
                var timeText = new TextBlock
                {
                    Text = $"🕒 Time: {block.StartTime} - {block.EndTime}",
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                blockPanel.Children.Add(timeText);
                
                // Days
                var daysText = new TextBlock
                {
                    Text = $"📅 Days: {GetDaysDescription(block.Days)}",
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                blockPanel.Children.Add(daysText);
                
                // Check if currently active
                if (IsBlockActive(block))
                {
                    var activeText = new TextBlock
                    {
                        Text = "🔴 CURRENTLY ACTIVE - PC is locked",
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F")),
                        Margin = new Thickness(0, 10, 0, 0)
                    };
                    blockPanel.Children.Add(activeText);
                    
                    // Calculate unlock time
                    var unlockTime = GetUnlockTime(block);
                    if (unlockTime.HasValue)
                    {
                        var unlockText = new TextBlock
                        {
                            Text = $"⏰ Will unlock at: {unlockTime.Value:HH:mm}",
                            FontSize = 14,
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                            Margin = new Thickness(0, 5, 0, 0)
                        };
                        blockPanel.Children.Add(unlockText);
                    }
                    
                    blockBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE"));
                    blockBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F"));
                }
                else
                {
                    // Check if upcoming today
                    var timeUntil = GetTimeUntilBlock(block);
                    if (timeUntil.HasValue && timeUntil.Value.TotalHours < 24)
                    {
                        var upcomingText = new TextBlock
                        {
                            Text = $"⏳ Upcoming in {FormatTimeSpan(timeUntil.Value)}",
                            FontSize = 14,
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F57C00")),
                            Margin = new Thickness(0, 10, 0, 0)
                        };
                        blockPanel.Children.Add(upcomingText);
                        
                        blockBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"));
                        blockBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
                    }
                }
                
                blockBorder.Child = blockPanel;
                SchedulePanel.Children.Add(blockBorder);
            }
        }
        
        private bool IsBlockActive(TimeBlock block)
        {
            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;
            var currentDay = now.DayOfWeek;
            
            if (!block.Days.Contains(currentDay))
                return false;
            
            TimeSpan startTime = TimeSpan.Parse(block.StartTime);
            TimeSpan endTime = TimeSpan.Parse(block.EndTime);
            
            if (startTime < endTime)
            {
                return currentTime >= startTime && currentTime <= endTime;
            }
            else
            {
                return currentTime >= startTime || currentTime <= endTime;
            }
        }
        
        private DateTime? GetUnlockTime(TimeBlock block)
        {
            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;
            
            TimeSpan endTime = TimeSpan.Parse(block.EndTime);
            TimeSpan startTime = TimeSpan.Parse(block.StartTime);
            
            if (startTime < endTime)
            {
                // Same day unlock
                return now.Date + endTime;
            }
            else
            {
                // Overnight block
                if (currentTime >= startTime)
                {
                    // Unlock is tomorrow
                    return now.Date.AddDays(1) + endTime;
                }
                else
                {
                    // Unlock is today
                    return now.Date + endTime;
                }
            }
        }
        
        private TimeSpan? GetTimeUntilBlock(TimeBlock block)
        {
            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;
            var currentDay = now.DayOfWeek;
            
            if (!block.Days.Contains(currentDay))
                return null;
            
            TimeSpan startTime = TimeSpan.Parse(block.StartTime);
            
            if (currentTime < startTime)
            {
                return startTime - currentTime;
            }
            
            return null;
        }
        
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
            }
            else
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
        }
        
        private string GetDaysDescription(List<DayOfWeek> days)
        {
            if (days.Count == 7)
                return "Every day";
            
            var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            var weekends = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday };
            
            if (days.Count == 5 && weekdays.All(d => days.Contains(d)))
                return "Weekdays (Mon-Fri)";
            
            if (days.Count == 2 && weekends.All(d => days.Contains(d)))
                return "Weekends (Sat-Sun)";
            
            var dayNames = days.Select(d => d.ToString().Substring(0, 3)).ToList();
            return string.Join(", ", dayNames);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Ensure we have an account configured
            var config = configManager.LoadConfig();
            if (string.IsNullOrWhiteSpace(config.AccountEmail))
            {
                StatusText.Text = "Please log in from the main window before refreshing the schedule.";
                return;
            }

            // Silently ensure we are authenticated using stored credentials
            bool authOk = await ServerSession.EnsureLoggedInAsync(configManager);
            if (!authOk)
            {
                StatusText.Text = "Could not authenticate with server; showing last known schedule.";
                LoadSchedule();
                return;
            }

            StatusText.Text = "Refreshing schedule from server...";

            bool ok = await SyncScheduleFromServer();
            LoadSchedule();

            if (ok)
            {
                StatusText.Text = "Time restrictions are enabled with the latest schedule from the server.";
            }
            else
            {
                StatusText.Text = "Failed to refresh schedule from server. Showing last known schedule.";
            }
        }

        private async Task<bool> SyncScheduleFromServer()
        {
            try
            {
                var blocks = await ServerSession.GetScheduleAsync();
                var config = configManager.LoadConfig();
                config.TimeBlocks = blocks;
                config.TimeRestrictionEnabled = blocks != null && blocks.Count > 0;
                configManager.SaveConfig(config);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

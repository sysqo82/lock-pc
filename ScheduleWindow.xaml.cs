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
                StatusText.Text = Loc.Instance.Strings.Sched_PleaseLogin;
                return;
            }
            
            if (!config.TimeRestrictionEnabled)
            {
                StatusText.Text = Loc.Instance.Strings.Sched_Disabled;
                
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
                    Text = "✓ " + Loc.Instance.Strings.Status_NoRestrictions,
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
                StatusText.Text = Loc.Instance.Strings.Sched_NoBlocks;
                
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
            
            StatusText.Text = string.Format(Loc.Instance.Strings.Sched_EnabledCount, config.TimeBlocks.Count);
            
            // Display each time block
            for (int i = 0; i < config.TimeBlocks.Count; i++)
            {
                var block = config.TimeBlocks[i];
                
                var blockBorder = new Border();
                var blockPanel = new StackPanel();
                
                // Block title
                var titleText = new TextBlock
                {
                    Text = string.Format(Loc.Instance.Strings.Sched_LockPeriod, i + 1),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F")),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                blockPanel.Children.Add(titleText);
                
                // Time range
                var timeText = new TextBlock
                {
                    Text = string.Format(Loc.Instance.Strings.Sched_Time, block.StartTime, block.EndTime),
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                blockPanel.Children.Add(timeText);
                
                // Days
                var daysText = new TextBlock
                {
                    Text = string.Format(Loc.Instance.Strings.Sched_Days, GetDaysDescription(block.Days)),
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                blockPanel.Children.Add(daysText);
                
                // Check if currently active
                if (IsBlockActive(block))
                {
                    var activeText = new TextBlock
                    {
                        Text = Loc.Instance.Strings.Sched_Active,
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
                            Text = string.Format(Loc.Instance.Strings.Sched_Upcoming, FormatTimeSpan(timeUntil.Value)),
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
            var s = Loc.Instance.Strings;
            if (days.Count == 7)
                return s.Days_EveryDay;
            
            var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            var weekends = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday };
            
            if (days.Count == 5 && weekdays.All(d => days.Contains(d)))
                return s.Days_Weekdays;
            
            if (days.Count == 2 && weekends.All(d => days.Contains(d)))
                return s.Days_Weekends;
            
            var dayNames = days.Select(d => Loc.Instance.Strings.GetDayName(d)).ToList();
            return string.Join(", ", dayNames);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Ensure we have an account configured
            var config = configManager.LoadConfig();
            if (string.IsNullOrWhiteSpace(config.AccountEmail))
            {
                StatusText.Text = Loc.Instance.Strings.Sched_PleaseLoginRefresh;
                return;
            }

            // Silently ensure we are authenticated using stored credentials
            bool authOk = await ServerSession.EnsureLoggedInAsync(configManager);
            if (!authOk)
            {
                StatusText.Text = Loc.Instance.Strings.Sched_AuthFailed;
                LoadSchedule();
                return;
            }

            StatusText.Text = Loc.Instance.Strings.Sched_Refreshing;

            bool ok = await SyncScheduleFromServer();
            LoadSchedule();

            if (ok)
            {
                StatusText.Text = Loc.Instance.Strings.Sched_EnabledLatest;
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

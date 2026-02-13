using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Linq;

namespace PCLockScreen
{
    public class ReminderDisplayItem
    {
        public string Title { get; set; }
        public string Time { get; set; }
    }

    public partial class LockScreenWindow : Window
    {
        private ConfigManager configManager;
        private DispatcherTimer timer;
        private ProcessProtection processProtection;

        // P/Invoke declarations to disable Task Manager and Alt+Tab
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID_CTRL_ALT_DEL = 1;
        private const int HOTKEY_ID_WIN_KEY = 2;
        private const int HOTKEY_ID_ALT_TAB = 3;
        private const int HOTKEY_ID_ALT_F4 = 4;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_WIN = 0x0008;
        private const uint VK_DELETE = 0x2E;
        private const uint VK_TAB = 0x09;
        private const uint VK_F4 = 0x73;
        private const uint VK_LWIN = 0x5B;

        public LockScreenWindow(ConfigManager configManager)
        {
            InitializeComponent();
            this.configManager = configManager;
            
            // Process protection is already active from MainWindow
            // Just track it for cleanup
            processProtection = new ProcessProtection();
            
            // Start timer to update display and check unlock conditions
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            UpdateDisplay();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Register hotkeys to block common key combinations
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var hwnd = helper.Handle;
            
            try
            {
                // Block Ctrl+Alt+Delete (may not work due to Windows security)
                RegisterHotKey(hwnd, HOTKEY_ID_CTRL_ALT_DEL, MOD_CONTROL | MOD_ALT, VK_DELETE);
                
                // Block Alt+Tab
                RegisterHotKey(hwnd, HOTKEY_ID_ALT_TAB, MOD_ALT, VK_TAB);
                
                // Block Alt+F4
                RegisterHotKey(hwnd, HOTKEY_ID_ALT_F4, MOD_ALT, VK_F4);
                
                // Block Windows key
                RegisterHotKey(hwnd, HOTKEY_ID_WIN_KEY, MOD_WIN, VK_LWIN);
            }
            catch
            {
                // Hotkey registration might fail, continue anyway
            }

            // Disable task manager ONLY during lock period
            ProcessProtection.DisableTaskManager(true);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateDisplay();
            CheckAutoUnlock();
        }

        private void UpdateDisplay()
        {
            CurrentTimeDisplay.Text = DateTime.Now.ToString("HH:mm:ss");
            
            var config = configManager.LoadConfig();
            if (config.TimeRestrictionEnabled)
            {
                var now = DateTime.Now;
                var currentTime = now.TimeOfDay;
                var currentDay = now.DayOfWeek;
                
                // Find the current blocking period
                TimeBlock currentBlock = null;
                foreach (var block in config.TimeBlocks)
                {
                    if (!block.Days.Contains(currentDay))
                        continue;

                    TimeSpan startTime = TimeSpan.Parse(block.StartTime);
                    TimeSpan endTime = TimeSpan.Parse(block.EndTime);

                    if (startTime < endTime)
                    {
                        if (currentTime >= startTime && currentTime <= endTime)
                        {
                            currentBlock = block;
                            break;
                        }
                    }
                    else
                    {
                        if (currentTime >= startTime || currentTime <= endTime)
                        {
                            currentBlock = block;
                            break;
                        }
                    }
                }
                
                if (currentBlock != null)
                {
                    TimeSpan startTime = TimeSpan.Parse(currentBlock.StartTime);
                    TimeSpan endTime = TimeSpan.Parse(currentBlock.EndTime);
                    DateTime unlockTime;
                    
                    if (startTime < endTime)
                    {
                        // Same day unlock
                        unlockTime = now.Date + endTime;
                    }
                    else
                    {
                        // Overnight block
                        if (currentTime >= startTime)
                        {
                            // Unlock is tomorrow
                            unlockTime = now.Date.AddDays(1) + endTime;
                        }
                        else
                        {
                            // Unlock is today
                            unlockTime = now.Date + endTime;
                        }
                    }
                    
                    StatusMessage.Text = $"PC will unlock at {unlockTime:HH:mm}";
                }
                else
                {
                    StatusMessage.Text = "This PC is currently locked. Enter admin password to unlock.";
                }
            }
            else
            {
                StatusMessage.Text = "This PC is currently locked. Enter admin password to unlock.";
            }
        }

        private void CheckAutoUnlock()
        {
            var config = configManager.LoadConfig();
            // If time restrictions are no longer enabled (for example,
            // the schedule was removed from the server while locked),
            // unlock immediately.
            if (!config.TimeRestrictionEnabled)
            {
                UnlockAndClose();
                return;
            }

            if (config.TimeRestrictionEnabled)
            {
                var now = DateTime.Now;
                var currentTime = now.TimeOfDay;
                var currentDay = now.DayOfWeek;
                
                bool isBlocked = false;
                
                foreach (var block in config.TimeBlocks)
                {
                    if (!block.Days.Contains(currentDay))
                        continue;

                    TimeSpan startTime = TimeSpan.Parse(block.StartTime);
                    TimeSpan endTime = TimeSpan.Parse(block.EndTime);

                    if (startTime < endTime)
                    {
                        if (currentTime >= startTime && currentTime <= endTime)
                        {
                            isBlocked = true;
                            break;
                        }
                    }
                    else
                    {
                        if (currentTime >= startTime || currentTime <= endTime)
                        {
                            isBlocked = true;
                            break;
                        }
                    }
                }
                
                if (!isBlocked)
                {
                    UnlockAndClose();
                }
            }
        }
        
        private bool IsInBlockedPeriod(LockConfig config)
        {
            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;
            var currentDay = now.DayOfWeek;
            
            foreach (var block in config.TimeBlocks)
            {
                if (!block.Days.Contains(currentDay))
                    continue;
                
                TimeSpan startTime = TimeSpan.Parse(block.StartTime);
                TimeSpan endTime = TimeSpan.Parse(block.EndTime);
                
                if (startTime < endTime)
                {
                    if (currentTime >= startTime && currentTime <= endTime)
                        return true;
                }
                else
                {
                    if (currentTime >= startTime || currentTime <= endTime)
                        return true;
                }
            }
            
            return false;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Block all Alt, Ctrl, and Windows key combinations
            if (e.Key == Key.LWin || e.Key == Key.RWin || 
                e.Key == Key.F4 || e.Key == Key.Tab ||
                (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt ||
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
            }
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Unlock_Click(sender, e);
            }
        }

        private async void Unlock_Click(object sender, RoutedEventArgs e)
        {
            var password = AdminPasswordInput.Password;
            
            if (string.IsNullOrEmpty(password))
            {
                ErrorMessage.Text = "Please enter a password.";
                return;
            }

            // Validate against the server using the configured account email.
            var config = configManager.LoadConfig();
            var email = config.AccountEmail;

            bool ok = false;
            try
            {
                ok = await ServerSession.ValidateCurrentUserPasswordAsync(password, email);
            }
            catch
            {
                ok = false;
            }

            // Fallback: if server validation fails or there is no active session,
            // allow using the locally-stored account password (if present).
            if (!ok)
            {
                try
                {
                    // Try encrypted stored account password first
                    if (configManager.TryGetAccountPassword(out var storedPwd) && !string.IsNullOrEmpty(storedPwd))
                    {
                        if (storedPwd == password)
                        {
                            ok = true;
                        }
                    }
                    // Also try legacy local password.dat if present
                    if (!ok && configManager.ValidatePassword(password))
                    {
                        ok = true;
                    }
                }
                catch { ok = false; }
            }

            if (ok)
            {
                UnlockAndClose();
            }
            else
            {
                ErrorMessage.Text = "Incorrect password. Access denied.";
                AdminPasswordInput.Clear();
            }
        }

        private void UnlockAndClose()
        {
            // Clean up
            timer.Stop();
            
            // Re-enable Task Manager when unlocking
            ProcessProtection.DisableTaskManager(false);
            
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var hwnd = helper.Handle;
            
            // Unregister hotkeys
            try
            {
                UnregisterHotKey(hwnd, HOTKEY_ID_CTRL_ALT_DEL);
                UnregisterHotKey(hwnd, HOTKEY_ID_ALT_TAB);
                UnregisterHotKey(hwnd, HOTKEY_ID_ALT_F4);
                UnregisterHotKey(hwnd, HOTKEY_ID_WIN_KEY);
            }
            catch { }
            
            // After a successful admin unlock, enter freeze mode so the
            // PC remains unlocked until the user explicitly resumes monitoring
            // from the tray menu. This prevents immediate or automatic re-locks.
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.EnterFreezeMode();
            }

            // Just close this window, don't shutdown the app
            // Process protection remains active from MainWindow
            this.Close();
        }

        public void UpdateReminders(List<ServerSession.Reminder> reminders)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    var displayItems = reminders.Select(r => new ReminderDisplayItem
                    {
                        Title = r.Title,
                        Time = r.Time
                    }).ToList();

                    RemindersListControl.ItemsSource = displayItems;
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update reminders display", ex);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Only prevent closing if timer is still running (not unlocked)
            if (timer != null && timer.IsEnabled)
            {
                e.Cancel = true;
            }
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace PCLockScreen
{
    public class TimeBlockViewModel : INotifyPropertyChanged
    {
        private int index;
        public int Index
        {
            get => index;
            set
            {
                index = value;
                OnPropertyChanged(nameof(Index));
                OnPropertyChanged(nameof(BlockNumber));
            }
        }
        
        public int BlockNumber => Index + 1;
        
        private string startTime = "22:00";
        public string StartTime
        {
            get => startTime;
            set { startTime = value; OnPropertyChanged(nameof(StartTime)); }
        }
        
        private string endTime = "08:00";
        public string EndTime
        {
            get => endTime;
            set { endTime = value; OnPropertyChanged(nameof(EndTime)); }
        }
        
        public bool Monday { get; set; } = true;
        public bool Tuesday { get; set; } = true;
        public bool Wednesday { get; set; } = true;
        public bool Thursday { get; set; } = true;
        public bool Friday { get; set; } = true;
        public bool Saturday { get; set; } = true;
        public bool Sunday { get; set; } = true;
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public partial class MainWindow : Window
    {
        private ConfigManager configManager;
        private NotifyIcon notifyIcon;
        private DispatcherTimer monitorTimer;
        private DispatcherTimer startupMonitorTimer;
        private DispatcherTimer schedulePollTimer;
        private DispatcherTimer statusReportTimer;
        private LockScreenWindow activeLockWindow = null;
        private DispatcherTimer cooldownTimer = null;
        private DispatcherTimer handshakeTimer = null;
        private DateTime? lastUnlockAt = null;
        private const int UnlockCooldownSeconds = 60;
        private bool warningShown = false; // 1-minute warning
        private bool fiveMinuteWarningShown = false; // 5-minute warning
        private ObservableCollection<TimeBlockViewModel> timeBlocks;
        private ProcessProtection processProtection;
        private bool freezeMode = false;
        private ToolStripMenuItem resumeMenuItem;
        private PcSocket pcSocket;
        private ScheduleWindow scheduleWindow;

        
        public MainWindow()
        {
            InitializeComponent();
            configManager = new ConfigManager();
            
            // Initialize empty collection
            timeBlocks = new ObservableCollection<TimeBlockViewModel>();
            // TimeBlocksControl is no longer interactive; collection is kept
            // only for potential read-only use.
            
            LoadConfiguration();
            UpdateStatus();
            InitializeTrayIcon();
            LoadAccountFromConfig();
            
            // If an account is already configured, start minimized to tray
            // to prevent tampering. On first run (no account), keep the
            // window visible so the user sees the login/register screen.
            var initialConfig = configManager.LoadConfig();
            if (!string.IsNullOrWhiteSpace(initialConfig.AccountEmail))
            {
                this.WindowState = WindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Hide();
                // Start periodic schedule polling for logged-in accounts
                StartSchedulePolling();
            }
            
            // Check for unexpected shutdown and auto-lock if needed
            CheckUnexpectedShutdown();
            
            // Set running flag
            configManager.SetRunningFlag();
            
            StartMonitoring();
            StartStartupMonitoring();
            StartStatusReporting();
            
            // Enable process protection on startup to make it hard to kill
            // But don't disable Task Manager - only disable during lock
            processProtection = new ProcessProtection();
            processProtection.EnableProtection();

            InitializeServerConnection();
        }

        private void LoadAccountFromConfig()
        {
            try
            {
                var config = configManager.LoadConfig();
                if (!string.IsNullOrWhiteSpace(config.AccountEmail))
                {
                    EmailTextBox.Text = config.AccountEmail;
                }
                UpdateAccountUI();
                    StartSchedulePolling();
                    StartHandshakePolling();
            }
            catch
            {
                // Ignore failures reading config; login UI will still work.
            }
        }

        private async void InitializeServerConnection()
        {
            try
            {
                var pcId = configManager.GetOrCreatePcId();
                var pcName = configManager.GetOrCreatePcName();

                pcSocket = new PcSocket(
                    ServerSession.BaseUrl,
                    pcId,
                    pcName,
                    OnServerCommandReceived,
                    OnServerScheduleUpdateReceived,
                    // Status provider: return current real status to avoid falsely reporting 'Unlocked' on reconnect
                    () =>
                    {
                        try
                        {
                            // Check if we're on the UI thread, if not, invoke on UI thread
                            if (!Dispatcher.CheckAccess())
                            {
                                return Dispatcher.Invoke(() =>
                                {
                                    // If a LockScreenWindow is currently visible, consider the PC locked
                                    foreach (Window w in System.Windows.Application.Current.Windows)
                                    {
                                        if (w is LockScreenWindow && w.IsVisible)
                                            return "Locked";
                                    }

                                    var cfg = configManager.LoadConfig();
                                    var inBlockedPeriod = IsInBlockedPeriod(cfg);
                                    var status = inBlockedPeriod ? "Locked" : "Unlocked";
                                    Logger.Log($"Status provider returning: {status} (inBlockedPeriod: {inBlockedPeriod})");
                                    return status;
                                });
                            }

                            // Already on UI thread
                            foreach (Window w in System.Windows.Application.Current.Windows)
                            {
                                if (w is LockScreenWindow && w.IsVisible)
                                    return "Locked";
                            }

                            var config = configManager.LoadConfig();
                            var blocked = IsInBlockedPeriod(config);
                            var currentStatus = blocked ? "Locked" : "Unlocked";
                            Logger.Log($"Status provider returning: {currentStatus} (inBlockedPeriod: {blocked})");
                            return currentStatus;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Status provider exception", ex);
                            return "Unknown";
                        }
                    }
                );
                await pcSocket.ConnectAsync();
            }
            catch
            {
                // Swallow connection errors to avoid impacting local locking behavior
            }
                        StartSchedulePolling();
        }

        private void OnServerCommandReceived(string action)
        {
            // Ensure any UI/lock operations run on the dispatcher thread
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (string.Equals(action, "lock", StringComparison.OrdinalIgnoreCase))
                {
                    ActivateLock();
                }
            }));
        }

        private void OnServerScheduleUpdateReceived()
        {
            // Ensure we marshal back to the UI thread before touching
            // configuration and UI elements.
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    // Best-effort: ensure we have an authenticated session
                    // and then pull the latest schedule from the server.
                    bool authOk = await ServerSession.EnsureLoggedInAsync(configManager);
                    if (!authOk)
                    {
                        return;
                    }

                    bool ok = await SyncScheduleFromServer();
                    if (ok)
                    {
                        LoadConfiguration();
                        UpdateStatus();

                        // If the schedule window is open, refresh it too.
                        if (scheduleWindow != null && scheduleWindow.IsVisible)
                        {
                            scheduleWindow.LoadSchedule();
                        }
                        // If the newly-fetched schedule indicates we should be
                        // locked right now, activate the lock immediately.
                        try
                        {
                            var cfg = configManager.LoadConfig();
                            if (cfg.TimeRestrictionEnabled && IsInBlockedPeriod(cfg))
                            {
                                ActivateLock();
                            }
                        }
                        catch
                        {
                            // Ignore lock activation failures here.
                        }
                    }
                }
                catch
                {
                    // Ignore failures; user can still refresh manually.
                }
            }));
        }

        private void CheckUnexpectedShutdown()
        {
            if (configManager.WasUnexpectedShutdown())
            {
                var config = configManager.LoadConfig();
                
                // If time restrictions are enabled and we're in a blocked period, auto-lock
                if (config.TimeRestrictionEnabled && configManager.IsPasswordSet())
                {
                    if (config.TimeBlocks.Count > 0 && IsInBlockedPeriod(config))
                    {
                        // Delay slightly to let window initialize
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ActivateLock();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }
            }
        }

        private void StartMonitoring()
        {
            // Check every 5 seconds if we need to auto-lock
            monitorTimer = new DispatcherTimer();
            monitorTimer.Interval = TimeSpan.FromSeconds(5);
            monitorTimer.Tick += MonitorTimer_Tick;
            monitorTimer.Start();
        }

        private void StartStartupMonitoring()
        {
            // Check every 5 seconds if startup is still enabled (protect against user disabling it)
            startupMonitorTimer = new DispatcherTimer();
            startupMonitorTimer.Interval = TimeSpan.FromSeconds(5);
            startupMonitorTimer.Tick += (s, e) =>
            {
                var config = configManager.LoadConfig();
                if (config.RunAtStartup)
                {
                    // Monitor and restore startup integrity
                    StartupManager.MonitorStartupIntegrity();
                }
            };
            startupMonitorTimer.Start();
        }

        private void StartSchedulePolling()
        {
            try
            {
                if (schedulePollTimer != null)
                    return;

                schedulePollTimer = new DispatcherTimer();
                schedulePollTimer.Interval = TimeSpan.FromSeconds(60);
                schedulePollTimer.Tick += async (s, e) =>
                {
                    try
                    {
                        bool authOk = await ServerSession.EnsureLoggedInAsync(configManager);
                        if (!authOk)
                            return;

                        bool ok = await SyncScheduleFromServer();
                        if (ok)
                        {
                            LoadConfiguration();
                            UpdateStatus();
                        }
                    }
                    catch
                    {
                        // Swallow polling errors; we'll try again on next tick
                    }
                };

                schedulePollTimer.Start();
            }
            catch
            {
                // Starting polling is best-effort
            }
        }

        private async Task EvaluateScheduleAndMaybeLock()
        {
            try
            {
                bool authOk = await ServerSession.EnsureLoggedInAsync(configManager);
                if (!authOk)
                {
                    Logger.Log("EvaluateSchedule: not authenticated; aborting");
                    return;
                }

                bool ok = await SyncScheduleFromServer();
                Logger.Log($"EvaluateSchedule: SyncScheduleFromServer returned {ok}");
                if (ok)
                {
                    LoadConfiguration();
                    UpdateStatus();

                    var cfg = configManager.LoadConfig();
                    if (cfg.TimeRestrictionEnabled && IsInBlockedPeriod(cfg))
                    {
                        Logger.Log("EvaluateSchedule: in blocked period — activating lock");
                        ActivateLock();
                    }
                    else
                    {
                        Logger.Log("EvaluateSchedule: not in blocked period — no action");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("EvaluateScheduleAndMaybeLock failed", ex);
            }
        }

        private void StopSchedulePolling()
        {
            try
            {
                if (schedulePollTimer != null)
                {
                    schedulePollTimer.Stop();
                    schedulePollTimer = null;
                }
            }
            catch
            {
                // ignore
            }
        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            // Skip monitoring if in freeze mode
            if (freezeMode)
            {
                Logger.Log("MonitorTick skipped: freezeMode active");
                return;
            }
            // Skip monitoring if we recently unlocked to prevent immediate relock
            if (lastUnlockAt.HasValue && (DateTime.Now - lastUnlockAt.Value).TotalSeconds < UnlockCooldownSeconds)
            {
                Logger.Log("MonitorTick skipped: within unlock cooldown");
                return;
            }
            
            var config = configManager.LoadConfig();
            Logger.Log($"MonitorTick: TimeRestrictionEnabled={config.TimeRestrictionEnabled}, PasswordSet={configManager.IsPasswordSet()}, BlockCount={config.TimeBlocks.Count}");
            
            if (config.TimeRestrictionEnabled && configManager.IsPasswordSet())
            {
                // If no blocks configured, don't lock
                if (config.TimeBlocks.Count == 0)
                {
                    Logger.Log("MonitorTick: No time blocks configured");
                    return;
                }
                
                var timeUntilLock = GetTimeUntilNextBlock(config);
                Logger.Log($"MonitorTick: timeUntilLock={(timeUntilLock.HasValue ? timeUntilLock.Value.TotalSeconds.ToString("F0") + " seconds" : "null")}, 5minWarningShown={fiveMinuteWarningShown}, 1minWarningShown={warningShown}");

                // Show first warning 5 minutes before lock (between 4-5 minutes window)
                if (timeUntilLock.HasValue && timeUntilLock.Value.TotalSeconds > 240 && 
                    timeUntilLock.Value.TotalSeconds <= 300 && !fiveMinuteWarningShown)
                {
                    Logger.Log("MonitorTick: Triggering 5-minute warning");
                    fiveMinuteWarningShown = true;
                    ToastNotificationHelper.ShowWarningToast(5);
                }

                // Show second, stronger warning 1 minute before lock
                if (timeUntilLock.HasValue && timeUntilLock.Value.TotalSeconds > 0 && 
                    timeUntilLock.Value.TotalSeconds <= 60 && !warningShown)
                {
                    Logger.Log("MonitorTick: Triggering 1-minute warning");
                    warningShown = true;
                    ToastNotificationHelper.ShowUrgentToast();
                }
                
                if (IsInBlockedPeriod(config))
                {
                    Logger.Log("MonitorTick: currently in blocked period — activating lock");
                    // Auto-activate lock (timer will be stopped in ActivateLock)
                    ActivateLock();
                }
                else
                {
                    Logger.Log("MonitorTick: not in blocked period");
                }
                
                // Reset warning flags if we're far from the next lock window
                if (timeUntilLock.HasValue && timeUntilLock.Value.TotalSeconds > 420)
                {
                    warningShown = false;
                    fiveMinuteWarningShown = false;
                }
            }
        }

        private void StartStatusReporting()
        {
            // Send status to server every 30 seconds
            statusReportTimer = new DispatcherTimer();
            statusReportTimer.Interval = TimeSpan.FromSeconds(30);
            statusReportTimer.Tick += (s, e) =>
            {
                try
                {
                    if (pcSocket != null)
                    {
                        // Check if lock screen is active
                        bool isLocked = false;
                        foreach (Window w in System.Windows.Application.Current.Windows)
                        {
                            if (w is LockScreenWindow && w.IsVisible)
                            {
                                isLocked = true;
                                break;
                            }
                        }

                        if (!isLocked)
                        {
                            var config = configManager.LoadConfig();
                            isLocked = IsInBlockedPeriod(config);
                        }

                        var status = isLocked ? "Locked" : "Unlocked";
                        _ = pcSocket.SendStatusAsync(status);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Status report timer error", ex);
                }
            };
            statusReportTimer.Start();
        }

        private TimeSpan? GetTimeUntilNextBlock(LockConfig config)
        {
            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;
            var currentDay = now.DayOfWeek;
            TimeSpan? minTimeUntil = null;

            // Check blocks for today
            foreach (var block in config.TimeBlocks)
            {
                // Check if this block applies to today
                if (block.Days.Contains(currentDay))
                {
                    TimeSpan startTime = TimeSpan.Parse(block.StartTime);
                    
                    if (currentTime < startTime)
                    {
                        var timeUntil = startTime - currentTime;
                        if (!minTimeUntil.HasValue || timeUntil < minTimeUntil.Value)
                        {
                            minTimeUntil = timeUntil;
                        }
                    }
                }
            }

            // If no blocks found today, check tomorrow's blocks (within next 24 hours)
            if (!minTimeUntil.HasValue)
            {
                var tomorrowDay = (DayOfWeek)(((int)currentDay + 1) % 7);
                foreach (var block in config.TimeBlocks)
                {
                    if (block.Days.Contains(tomorrowDay))
                    {
                        TimeSpan startTime = TimeSpan.Parse(block.StartTime);
                        // Time until tomorrow's block = remaining today + time to block tomorrow
                        var timeUntilMidnight = TimeSpan.FromHours(24) - currentTime;
                        var timeUntil = timeUntilMidnight + startTime;
                        
                        if (!minTimeUntil.HasValue || timeUntil < minTimeUntil.Value)
                        {
                            minTimeUntil = timeUntil;
                        }
                    }
                }
            }

            return minTimeUntil;
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
                    // Normal case: blocked from startTime to endTime
                    if (currentTime >= startTime && currentTime <= endTime)
                        return true;
                }
                else
                {
                    // Overnight case: blocked from startTime to endTime next day
                    if (currentTime >= startTime || currentTime <= endTime)
                        return true;
                }
            }
            
            return false;
        }

        private void InitializeTrayIcon()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Shield; // Using system icon
            notifyIcon.Text = "PC Lock Screen";
            notifyIcon.Visible = true; // Always visible for notifications

            // Create context menu
            var contextMenu = new ContextMenuStrip();
            var showItem = new ToolStripMenuItem("Show Configuration");
            showItem.Click += (s, e) => ShowWindowWithPassword();
            
            var scheduleItem = new ToolStripMenuItem("Show Schedule");
            scheduleItem.Click += (s, e) => ShowSchedule();
            
            // Resume menu item - only visible in freeze mode
            resumeMenuItem = new ToolStripMenuItem("Resume Monitoring");
            resumeMenuItem.Click += (s, e) => ResumeMonitoring();
            resumeMenuItem.Visible = false;
            
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitWithPassword();

            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(scheduleItem);
            contextMenu.Items.Add(resumeMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);
            notifyIcon.ContextMenuStrip = contextMenu;

            // Double-click to restore
            notifyIcon.DoubleClick += (s, e) => ShowWindowWithPassword();
        }

        private void ShowWindowWithPassword()
        {
            if (!configManager.IsPasswordSet())
            {
                ShowWindow();
                return;
            }

            var passwordWindow = new PasswordDialog(configManager);
            if (passwordWindow.ShowDialog() == true)
            {
                ShowWindow();
            }
        }

        private void ExitWithPassword()
        {
            if (!configManager.IsPasswordSet())
            {
                ExitApplication();
                return;
            }

            var passwordWindow = new PasswordDialog(configManager);
            if (passwordWindow.ShowDialog() == true)
            {
                ExitApplication();
            }
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ShowSchedule()
        {
            if (scheduleWindow == null || !scheduleWindow.IsVisible)
            {
                scheduleWindow = new ScheduleWindow(configManager);
                scheduleWindow.Closed += (s, e) => scheduleWindow = null;
                scheduleWindow.Show();
            }
            else
            {
                scheduleWindow.Activate();
            }
        }
        
        public void EnterFreezeMode()
        {
            freezeMode = true;
            if (resumeMenuItem != null)
            {
                resumeMenuItem.Visible = true;
            }
            
            try
            {
                notifyIcon.ShowBalloonTip(
                    5000,
                    "Freeze Mode Activated",
                    "Automatic locking is paused. Click 'Resume Monitoring' in the tray menu to resume.",
                    ToolTipIcon.Info
                );
            }
            catch { }
        }
        
        private void ResumeMonitoring()
        {
            freezeMode = false;
            warningShown = false;
            fiveMinuteWarningShown = false;
            if (resumeMenuItem != null)
            {
                resumeMenuItem.Visible = false;
            }
            
            try
            {
                notifyIcon.ShowBalloonTip(
                    3000,
                    "Monitoring Resumed",
                    "Automatic locking has been resumed.",
                    ToolTipIcon.Info
                );
            }
            catch { }
        }

        private void ExitApplication()
        {
            // Clear running flag - this is a graceful shutdown
            configManager.ClearRunningFlag();
            
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            
            // Disable process protection on legitimate exit
            if (processProtection != null)
            {
                processProtection.DisableProtection();
            }

            // Best-effort socket disconnect
            if (pcSocket != null)
            {
                _ = pcSocket.DisconnectAsync();
            }
            
            // Make sure Task Manager is re-enabled
            ProcessProtection.DisableTaskManager(false);
            
            System.Windows.Application.Current.Shutdown();
        }

        private async Task RegisterPcWithServerAsync()
        {
            try
            {
                var pcId = configManager.GetOrCreatePcId();
                var pcName = configManager.GetOrCreatePcName();
                var localIp = GetLocalIpAddress();

                // Register over HTTP so the server can persist ownership
                // and trigger schedule_update for this PC.
                await ServerSession.RegisterPcAsync(pcId, pcName, localIp);

                // Also re-send the register_pc event over the socket in
                // case the socket was connected before login.
                if (pcSocket != null)
                {
                    await pcSocket.RegisterPcWithSocketAsync();
                }
            }
            catch
            {
                // Best-effort only; failures here shouldn't break login.
            }
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                // Prefer active physical interfaces (Wi-Fi / Ethernet) and skip virtual adapters
                var ifs = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

                // Sort interfaces so Wireless and Ethernet are preferred
                Array.Sort(ifs, (a, b) =>
                {
                    int score(System.Net.NetworkInformation.NetworkInterfaceType t)
                    {
                        if (t == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211) return 0;
                        if (t == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet) return 1;
                        return 2;
                    }

                    return score(a.NetworkInterfaceType).CompareTo(score(b.NetworkInterfaceType));
                });

                foreach (var ni in ifs)
                {
                    try
                    {
                        if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                            continue;

                        var name = (ni.Name ?? string.Empty).ToLowerInvariant();
                        var desc = (ni.Description ?? string.Empty).ToLowerInvariant();

                        // Heuristic: skip commonly-named virtual adapters and tunneling/NAT adapters
                        var skipKeywords = new[] { "virtual", "vbox", "virtualbox", "vmware", "host-only", "vethernet", "hyper-v", "hyperv", "wsl", "docker", "nat", "bridge", "tap", "tunnel" };
                        bool skip = false;
                        foreach (var kw in skipKeywords)
                        {
                            if (name.Contains(kw) || desc.Contains(kw))
                            {
                                skip = true;
                                break;
                            }
                        }

                        if (skip)
                            continue;

                        var ipProps = ni.GetIPProperties();
                        foreach (var ua in ipProps.UnicastAddresses)
                        {
                            var ip = ua.Address;
                            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                var s = ip.ToString();
                                // Skip loopback and APIPA addresses
                                if (s.StartsWith("127.") || s.StartsWith("169."))
                                    continue;

                                return s;
                            }
                        }
                    }
                    catch { }
                }

                // Fallback: DNS lookup (previous behavior)
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        var s = ip.ToString();
                        if (s.StartsWith("127.") || s.StartsWith("169."))
                            continue;
                        return s;
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private void LoadConfiguration()
        {
            var config = configManager.LoadConfig();
            RunAtStartup.IsChecked = config.RunAtStartup;
            
            // Time blocks themselves are now managed by the server; we keep
            // the local collection only for potential read-only display.
            timeBlocks.Clear();
            foreach (var block in config.TimeBlocks)
            {
                var viewModel = new TimeBlockViewModel
                {
                    Index = timeBlocks.Count,
                    StartTime = block.StartTime,
                    EndTime = block.EndTime,
                    Monday = block.Days.Contains(DayOfWeek.Monday),
                    Tuesday = block.Days.Contains(DayOfWeek.Tuesday),
                    Wednesday = block.Days.Contains(DayOfWeek.Wednesday),
                    Thursday = block.Days.Contains(DayOfWeek.Thursday),
                    Friday = block.Days.Contains(DayOfWeek.Friday),
                    Saturday = block.Days.Contains(DayOfWeek.Saturday),
                    Sunday = block.Days.Contains(DayOfWeek.Sunday)
                };
                timeBlocks.Add(viewModel);
            }
        }

        private void RunAtStartup_Changed(object sender, RoutedEventArgs e)
        {
            bool shouldRunAtStartup = RunAtStartup.IsChecked == true;
            
            try
            {
                if (shouldRunAtStartup)
                {
                    StartupManager.EnableStartup();
                }
                else
                {
                    StartupManager.DisableStartup();
                }
                
                // Save preference to config
                var config = configManager.LoadConfig();
                config.RunAtStartup = shouldRunAtStartup;
                configManager.SaveConfig(config);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to update startup settings: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                
                // Revert checkbox state
                RunAtStartup.IsChecked = !shouldRunAtStartup;
            }
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            var email = (EmailTextBox.Text ?? string.Empty).Trim();
            var password = AccountPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                AccountStatusText.Text = "Please enter both email and password.";
                return;
            }

            AccountStatusText.Text = "Logging in...";

            try
            {
                bool ok = await ServerSession.LoginAsync(email, password);
                if (ok)
                {
                    configManager.SaveAccountCredentials(email, password);

                    // Associate this PC with the logged-in user and
                    // trigger an immediate schedule_update from the server.
                    await RegisterPcWithServerAsync();

                    bool scheduleOk = await SyncScheduleFromServer();
                    LoadConfiguration();
                    UpdateStatus();

                    if (scheduleOk)
                    {
                        AccountStatusText.Text = "Logged in and schedule synced from server.";
                    }
                    else
                    {
                        AccountStatusText.Text = "Logged in, but failed to load schedule from server.";
                    }

                    UpdateAccountUI();
                        StartSchedulePolling();
                        StartHandshakePolling();
                }
                else
                {
                    AccountStatusText.Text = "Login failed. Check your email and password.";
                }
            }
            catch (Exception ex)
            {
                AccountStatusText.Text = $"Login error: {ex.Message}";
            }
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            var email = (EmailTextBox.Text ?? string.Empty).Trim();
            var password = AccountPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                AccountStatusText.Text = "Please enter both email and password.";
                return;
            }

            AccountStatusText.Text = "Registering account...";

            try
            {
                bool ok = await ServerSession.RegisterAndLoginAsync(email, password);
                if (ok)
                {
                    configManager.SaveAccountCredentials(email, password);

                    // Associate this PC with the new account.
                    await RegisterPcWithServerAsync();

                    bool scheduleOk = await SyncScheduleFromServer();
                    LoadConfiguration();
                    UpdateStatus();

                    if (scheduleOk)
                    {
                        AccountStatusText.Text = "Account created, logged in, and schedule synced from server.";
                    }
                    else
                    {
                        AccountStatusText.Text = "Account created and logged in, but failed to load schedule from server.";
                    }

                    UpdateAccountUI();
                    StartSchedulePolling();
                    StartHandshakePolling();
                }
                else
                {
                    AccountStatusText.Text = "Registration or login failed. The email may already be in use or credentials are invalid.";
                }
            }
            catch (Exception ex)
            {
                AccountStatusText.Text = $"Registration error: {ex.Message}";
            }
        }

        private async Task<bool> SyncScheduleFromServer()
        {
            try
            {
                var blocks = await ServerSession.GetScheduleAsync();
                Logger.Log($"SyncScheduleFromServer: fetched { (blocks==null?0:blocks.Count) } blocks");
                if (blocks != null)
                {
                    for (int i = 0; i < blocks.Count; i++)
                    {
                        var b = blocks[i];
                        var days = b.Days != null ? string.Join(',', b.Days) : "(none)";
                        Logger.Log($"Block[{i}]: {b.StartTime}-{b.EndTime} Days:{days}");
                    }
                }

                var config = configManager.LoadConfig();
                // If server returned null, indicate failure.
                if (blocks == null)
                {
                    Logger.Log("SyncScheduleFromServer: server returned null (error)");
                    return false;
                }

                // If server returned zero blocks, do not overwrite the local schedule
                // to avoid losing the last-known schedule due to transient server issues.
                if (blocks.Count == 0)
                {
                    Logger.Log("SyncScheduleFromServer: fetched 0 blocks; preserving existing local schedule");
                    return true;
                }

                // Normal case: update local schedule
                config.TimeBlocks = blocks;
                config.TimeRestrictionEnabled = blocks.Count > 0;
                configManager.SaveConfig(config);
                Logger.Log($"SyncScheduleFromServer: saved config. Now={DateTime.Now:O}");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateAccountUI()
        {
            try
            {
                var config = configManager.LoadConfig();
                var email = config.AccountEmail;

                if (!string.IsNullOrWhiteSpace(email))
                {
                    LoggedOutPanel.Visibility = Visibility.Collapsed;
                    LoggedInPanel.Visibility = Visibility.Visible;
                    LoggedInEmailText.Text = email;
                }
                else
                {
                    LoggedOutPanel.Visibility = Visibility.Visible;
                    LoggedInPanel.Visibility = Visibility.Collapsed;
                    LoggedInEmailText.Text = string.Empty;
                }
            }
            catch
            {
                // If anything goes wrong, fall back to logged-out view.
                LoggedOutPanel.Visibility = Visibility.Visible;
                LoggedInPanel.Visibility = Visibility.Collapsed;
                LoggedInEmailText.Text = string.Empty;
            }
        }

        private async void RefreshSchedule_Click(object sender, RoutedEventArgs e)
        {
            AccountStatusText.Text = "Refreshing schedule from server...";
            bool authOk = await ServerSession.EnsureLoggedInAsync(configManager);
            if (!authOk)
            {
                AccountStatusText.Text = "Could not authenticate with server; showing last known schedule.";
                return;
            }

            bool ok = await SyncScheduleFromServer();
            LoadConfiguration();
            UpdateStatus();
            AccountStatusText.Text = ok
                ? "Schedule refreshed from server."
                : "Failed to refresh schedule from server.";
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            // Clear local account and session
            configManager.SaveAccountEmail(string.Empty);
            ServerSession.Logout();

            EmailTextBox.Text = string.Empty;
            AccountPasswordBox.Password = string.Empty;
            AccountStatusText.Text = "Logged out.";

            UpdateAccountUI();
            UpdateStatus();
            StopSchedulePolling();
            StopHandshakePolling();
        }

        private void StartHandshakePolling()
        {
            try
            {
                if (handshakeTimer != null)
                    return;

                handshakeTimer = new DispatcherTimer();
                handshakeTimer.Interval = TimeSpan.FromSeconds(15);
                handshakeTimer.Tick += async (s, e) =>
                {
                    try
                    {
                        // Ensure we have valid credentials/session
                        bool authOk = await ServerSession.EnsureLoggedInAsync(configManager);
                        if (!authOk)
                            return;

                        // Ensure socket is connected so the server can associate the socket
                        // with the authenticated PC. Then re-register via HTTP to refresh mapping.
                        try
                        {
                            if (pcSocket != null)
                            {
                                await pcSocket.ConnectAsync();
                                // Small delay to let socket handshake complete
                                await Task.Delay(250);
                            }
                        }
                        catch { }

                        await RegisterPcWithServerAsync();
                    }
                    catch
                    {
                        // Swallow errors; we'll retry on next tick
                    }
                };
                handshakeTimer.Start();
            }
            catch { }
        }

        private void StopHandshakePolling()
        {
            try
            {
                if (handshakeTimer != null)
                {
                    handshakeTimer.Stop();
                    handshakeTimer = null;
                }
            }
            catch { }
        }

        private void ActivateLock_Click(object sender, RoutedEventArgs e)
        {
            if (!configManager.IsPasswordSet())
            {
                MessageBox.Show("Please set an admin password first.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ActivateLock();
        }

        private void ActivateLock()
        {
            // Ensure we only ever create a single LockScreenWindow instance
            try
            {
                if (activeLockWindow != null && activeLockWindow.IsVisible)
                {
                    try
                    {
                        Logger.Log("ActivateLock called but LockScreenWindow already visible — bringing to front");
                        activeLockWindow.Activate();
                        activeLockWindow.Topmost = true; // ensure on top briefly
                        activeLockWindow.Topmost = false;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to bring existing LockScreenWindow to front", ex);
                    }
                    return; // don't create a new lock window
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error checking for existing LockScreenWindow", ex);
            }
            // Stop monitoring timer before locking
            if (monitorTimer != null)
            {
                monitorTimer.Stop();
            }
            warningShown = false; // Reset for next time
            fiveMinuteWarningShown = false;

            activeLockWindow = new LockScreenWindow(configManager);
            Logger.Log("Activating lock window");

            // Send status update to server
            pcSocket?.SendStatusAsync("Locked").ConfigureAwait(false);

            activeLockWindow.Closed += (s, e) =>
            {
                try
                {
                    Logger.Log("Lock window closed by user");

                    // Record unlock time to avoid immediate re-lock from monitor
                    lastUnlockAt = DateTime.Now;

                    // When lock window closes, restart monitoring (reuse existing timer)
                    if (monitorTimer != null)
                    {
                        monitorTimer.Start();
                    }

                    // Send status update to server
                    pcSocket?.SendStatusAsync("Unlocked").ConfigureAwait(false);

                    // Schedule a forced re-evaluation after the unlock cooldown so
                    // we don't rely solely on the monitor tick timing. Only one
                    // cooldown timer will be active at any time.
                    try
                    {
                        if (cooldownTimer != null)
                        {
                            cooldownTimer.Stop();
                            cooldownTimer = null;
                        }

                        cooldownTimer = new DispatcherTimer();
                        cooldownTimer.Interval = TimeSpan.FromSeconds(UnlockCooldownSeconds);
                        cooldownTimer.Tick += async (ts, te) =>
                        {
                            try
                            {
                                cooldownTimer.Stop();
                                cooldownTimer = null;
                                Logger.Log("Cooldown expired — re-evaluating schedule for possible re-lock");
                                await EvaluateScheduleAndMaybeLock();
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("Error during cooldown re-eval", ex);
                            }
                        };
                        cooldownTimer.Start();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to start cooldown timer", ex);
                    }

                    // Don't show MainWindow - keep it hidden for security
                    // User can access it from system tray if needed
                }
                finally
                {
                    // Clear reference to the active lock window
                    activeLockWindow = null;
                }
            };

            activeLockWindow.Show();
            this.Hide();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Minimize to tray instead of closing
            e.Cancel = true;
            this.Hide();
            notifyIcon.ShowBalloonTip(2000, "PC Lock Screen", "Application minimized to tray. Double-click to restore.", ToolTipIcon.Info);
        }

        private void UpdateStatus()
        {
            var config = configManager.LoadConfig();

            // If no account is configured, hide schedule details.
            if (string.IsNullOrWhiteSpace(config.AccountEmail))
            {
                StatusText.Text = "Not connected to server";
                TimeStatusText.Text = "Log in to your account to load the lock schedule.";
                return;
            }
            
            if (config.TimeRestrictionEnabled)
            {
                if (config.TimeBlocks.Count == 0)
                {
                    StatusText.Text = "Time Restrictions Enabled";
                    TimeStatusText.Text = "No blocks configured - PC is unrestricted";
                }
                else
                {
                    StatusText.Text = "Time Restrictions Enabled";
                    
                    var blockDescriptions = new List<string>();
                    foreach (var block in config.TimeBlocks)
                    {
                        string days = GetDaysDescription(block.Days);
                        blockDescriptions.Add($"{block.StartTime}-{block.EndTime} ({days})");
                    }
                    
                    TimeStatusText.Text = string.Join("\n", blockDescriptions);
                }
            }
            else
            {
                StatusText.Text = "No Time Restrictions";
                TimeStatusText.Text = "PC can be used at any time";
            }
        }
        
        private string GetDaysDescription(List<DayOfWeek> days)
        {
            if (days.Count == 7)
                return "Every day";
            
            var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            var weekends = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday };
            
            if (days.Count == 5 && weekdays.All(d => days.Contains(d)))
                return "Weekdays";
            
            if (days.Count == 2 && weekends.All(d => days.Contains(d)))
                return "Weekends";
            
            var dayNames = days.Select(d => d.ToString().Substring(0, 3)).ToList();
            return string.Join(", ", dayNames);
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace PCLockScreen
{
    public class StartupManager
    {
        private const string TASK_NAME = "PCLockScreenAutoStart";
        private const string REGISTRY_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "PCLockScreen";
        
        public static bool IsRunAsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                // Check both scheduled task and registry for backwards compatibility
                return IsScheduledTaskEnabled() || IsRegistryStartupEnabled();
            }
            catch
            {
                return false;
            }
        }

        private static bool IsScheduledTaskEnabled()
        {
            try
            {
                using (TaskService ts = new TaskService())
                {
                    var task = ts.GetTask(TASK_NAME);
                    return task != null && task.Enabled;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRegistryStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false))
                {
                    return key?.GetValue(APP_NAME) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void EnableStartup()
        {
            try
            {
                // Use AppContext.BaseDirectory for single-file apps
                string exePath = Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName);
                if (!exePath.EndsWith(".exe"))
                {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName ?? exePath;
                }

                // ONLY create scheduled task (doesn't show in Task Manager startup list)
                // Don't add registry entry - it shows in Task Manager and can be disabled
                CreateScheduledTask(exePath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to enable startup: {ex.Message}\n\nThis feature requires administrator privileges.",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error
                );
            }
        }

        public static void DisableStartup()
        {
            try
            {
                // Remove scheduled task
                RemoveScheduledTask();
                
                // Also remove any legacy registry entry
                RemoveFromRegistry();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to disable startup: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error
                );
            }
        }

        private static void CreateScheduledTask(string exePath)
        {
            using (TaskService ts = new TaskService())
            {
                // Remove existing task if present
                ts.RootFolder.DeleteTask(TASK_NAME, false);

                // Create new task definition
                TaskDefinition td = ts.NewTask();
                td.RegistrationInfo.Description = "Starts PC Lock Screen application at user logon";
                td.Principal.RunLevel = TaskRunLevel.Highest; // Run with highest privileges
                
                // Trigger: Run at logon
                td.Triggers.Add(new LogonTrigger 
                { 
                    UserId = WindowsIdentity.GetCurrent().Name 
                });

                // Action: Start the application
                td.Actions.Add(new ExecAction(exePath, null, Path.GetDirectoryName(exePath)));

                // Settings
                td.Settings.DisallowStartIfOnBatteries = false;
                td.Settings.StopIfGoingOnBatteries = false;
                td.Settings.ExecutionTimeLimit = TimeSpan.Zero; // No time limit
                td.Settings.AllowDemandStart = true;
                td.Settings.StartWhenAvailable = true;
                td.Settings.Hidden = true; // Hide from casual view

                // Register the task
                ts.RootFolder.RegisterTaskDefinition(
                    TASK_NAME,
                    td,
                    TaskCreation.CreateOrUpdate,
                    null, // Use current user
                    null, // No password needed for current user
                    TaskLogonType.InteractiveToken
                );
            }
        }

        private static void RemoveScheduledTask()
        {
            using (TaskService ts = new TaskService())
            {
                ts.RootFolder.DeleteTask(TASK_NAME, false);
            }
        }

        private static void AddToRegistry(string exePath)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
            {
                key?.SetValue(APP_NAME, $"\"{exePath}\"");
            }
        }

        private static void RemoveFromRegistry()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
            {
                if (key?.GetValue(APP_NAME) != null)
                {
                    key.DeleteValue(APP_NAME, false);
                }
            }
        }

        // Monitor and protect the scheduled task from being disabled
        public static void MonitorStartupIntegrity()
        {
            try
            {
                // Check if scheduled task exists and is enabled
                if (!IsScheduledTaskEnabled())
                {
                    // Re-enable it
                    string exePath = Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName);
                    if (!exePath.EndsWith(".exe"))
                    {
                        exePath = Process.GetCurrentProcess().MainModule?.FileName ?? exePath;
                    }
                    CreateScheduledTask(exePath);
                }
            }
            catch
            {
                // Silent fail - don't interrupt user
            }
        }
    }
}

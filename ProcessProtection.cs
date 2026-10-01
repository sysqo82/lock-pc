using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace PCLockScreen
{
    public class ProcessProtection
    {
        public static void DisableTaskManager(bool disable)
        {
            try
            {
                // Modify registry to disable Task Manager
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
                
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        if (disable)
                        {
                            key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                        }
                        else
                        {
                            key.DeleteValue("DisableTaskMgr", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Task Manager disable failed: {ex.Message}");
            }
        }

        public static void DisableRegistryEditor(bool disable)
        {
            try
            {
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
                
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        if (disable)
                        {
                            key.SetValue("DisableRegistryTools", 1, RegistryValueKind.DWord);
                        }
                        else
                        {
                            key.DeleteValue("DisableRegistryTools", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Registry Editor disable failed: {ex.Message}");
            }
        }
    }
}

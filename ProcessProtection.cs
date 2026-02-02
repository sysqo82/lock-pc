using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PCLockScreen
{
    public class ProcessProtection
    {
        // P/Invoke declarations for process protection
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetInformationProcess(IntPtr processHandle, int processInformationClass, ref int processInformation, int processInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int PROCESS_SET_INFORMATION = 0x0200;
        private const int ProcessBreakOnTermination = 29; // BreakOnTermination

        private bool isProtectionEnabled = false;

        public void EnableProtection()
        {
            try
            {
                // Set process as critical system process
                // WARNING: This will cause a BSOD (Blue Screen) if the process is forcefully terminated
                // This is intentional to prevent any termination attempts
                IntPtr processHandle = GetCurrentProcess();
                int isCritical = 1;
                
                int status = NtSetInformationProcess(processHandle, ProcessBreakOnTermination, ref isCritical, sizeof(int));
                
                if (status >= 0)
                {
                    isProtectionEnabled = true;
                    Debug.WriteLine("Process protection enabled - process is now critical");
                }
                else
                {
                    Debug.WriteLine($"Failed to enable process protection. Status: {status}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Protection enable failed: {ex.Message}");
            }
        }

        public void DisableProtection()
        {
            try
            {
                if (isProtectionEnabled)
                {
                    // Disable critical process status before exiting
                    IntPtr processHandle = GetCurrentProcess();
                    int isCritical = 0;
                    
                    NtSetInformationProcess(processHandle, ProcessBreakOnTermination, ref isCritical, sizeof(int));
                    isProtectionEnabled = false;
                    Debug.WriteLine("Process protection disabled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Protection disable failed: {ex.Message}");
            }
        }

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

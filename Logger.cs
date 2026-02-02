using System;
using System.IO;
using System.Text;
using System.Threading;

namespace PCLockScreen
{
    public static class Logger
    {
        private static readonly object LockObj = new object();
        private static string logPath;

        static Logger()
        {
            // In DEBUG builds, write logs next to the binary (debug folder) for easy access.
#if DEBUG
            try
            {
                var dir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                Directory.CreateDirectory(dir);
                logPath = Path.Combine(dir, "logs.txt");
                return;
            }
            catch
            {
                // fall back to normal behavior below
            }
#endif
            // Try multiple sensible locations in order: Roaming AppData, Local AppData, executable folder
            var candidates = new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppContext.BaseDirectory
            };

            foreach (var baseDir in candidates)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(baseDir))
                        continue;

                    var dir = Path.Combine(baseDir, "PCLockScreen");
                    Directory.CreateDirectory(dir);
                    logPath = Path.Combine(dir, "logs.txt");
                    // sanity-check: try writing an empty line (no-op) to ensure writable
                    using (var fs = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read)) { }
                    return;
                }
                catch (Exception ex)
                {
                    try { System.Diagnostics.Debug.WriteLine($"Logger init failed for '{baseDir}': {ex.Message}"); } catch { }
                    // try next candidate
                }
            }

            // Fallback to current directory
            try
            {
                logPath = Path.Combine(Directory.GetCurrentDirectory(), "logs.txt");
            }
            catch
            {
                logPath = "logs.txt";
            }
        }

        public static void Log(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                lock (LockObj)
                {
                    try
                    {
                        var dir = Path.GetDirectoryName(logPath);
                        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        File.AppendAllText(logPath, line, Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        try { System.Diagnostics.Debug.WriteLine($"Logger write failed: {ex.Message}"); } catch { }
                    }
                }
            }
            catch
            {
                // swallow logging errors to avoid interfering with app
            }
        }

        public static void LogError(string message, Exception ex = null)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR: {message}";
                if (ex != null)
                    line += $" Exception: {ex}\n";
                line += Environment.NewLine;
                lock (LockObj)
                {
                    try
                    {
                        var dir = Path.GetDirectoryName(logPath);
                        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        File.AppendAllText(logPath, line, Encoding.UTF8);
                    }
                    catch (Exception dex)
                    {
                        try { System.Diagnostics.Debug.WriteLine($"Logger error write failed: {dex.Message}"); } catch { }
                    }
                }
            }
            catch
            {
            }
        }

        public static string GetLogPath() => logPath;
    }
}

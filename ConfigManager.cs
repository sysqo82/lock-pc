using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace PCLockScreen
{
    public class TimeBlock
    {
        public string StartTime { get; set; } = "22:00";
        public string EndTime { get; set; } = "08:00";
        public List<DayOfWeek> Days { get; set; } = new List<DayOfWeek> 
        { 
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday 
        };
    }

    public class LockConfig
    {
        public bool TimeRestrictionEnabled { get; set; }
        public List<TimeBlock> TimeBlocks { get; set; } = new List<TimeBlock> 
        { 
            new TimeBlock() 
        };
        public bool RunAtStartup { get; set; }
        // Optional runtime-overridable server base URL. If empty, client uses built-in default.
        public string ServerBaseUrl { get; set; } = string.Empty;
        public string PcId { get; set; } = string.Empty;
        public string PcName { get; set; } = string.Empty;
        public string AccountEmail { get; set; } = string.Empty;
        public string EncryptedAccountPassword { get; set; } = string.Empty;
    }

    public class ConfigManager
    {
        private readonly string configDirectory;
        private readonly string configFilePath;
        private readonly string passwordFilePath;
        private readonly string shutdownFlagPath;

        public ConfigManager()
        {
            configDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PCLockScreen"
            );
            configFilePath = Path.Combine(configDirectory, "config.json");
            passwordFilePath = Path.Combine(configDirectory, "password.dat");
            shutdownFlagPath = Path.Combine(configDirectory, "running.flag");

            // Ensure directory exists
            if (!Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }
        }

        public string GetOrCreatePcId()
        {
            var config = LoadConfig();
            if (string.IsNullOrWhiteSpace(config.PcId))
            {
                // Generate a deterministic PC ID based on machine name
                // This ensures the same PC gets the same ID even after reinstall/config deletion
                config.PcId = GenerateDeterministicPcId();
                SaveConfig(config);
            }

            return config.PcId;
        }

        private string GenerateDeterministicPcId()
        {
            // Use machine name as the seed for a deterministic GUID
            // This ensures the same PC always gets the same ID
            string machineName = Environment.MachineName;
            
            // Create a namespace GUID (could be any constant)
            Guid namespaceId = new Guid("a1b2c3d4-e5f6-7890-abcd-1234567890ab");
            
            // Generate deterministic GUID based on machine name
            using (var md5 = MD5.Create())
            {
                byte[] namespaceBytes = namespaceId.ToByteArray();
                byte[] nameBytes = Encoding.UTF8.GetBytes(machineName.ToUpperInvariant());
                byte[] combined = new byte[namespaceBytes.Length + nameBytes.Length];
                
                Buffer.BlockCopy(namespaceBytes, 0, combined, 0, namespaceBytes.Length);
                Buffer.BlockCopy(nameBytes, 0, combined, namespaceBytes.Length, nameBytes.Length);
                
                byte[] hash = md5.ComputeHash(combined);
                
                // Create a GUID from the hash
                return new Guid(hash).ToString();
            }
        }

        public string GetOrCreatePcName()
        {
            var config = LoadConfig();
            if (string.IsNullOrWhiteSpace(config.PcName))
            {
                config.PcName = Environment.MachineName;
                SaveConfig(config);
            }

            return config.PcName;
        }

        public LockConfig LoadConfig()
        {
            if (!File.Exists(configFilePath))
            {
                return new LockConfig();
            }

            try
            {
                string json = File.ReadAllText(configFilePath);
                return JsonSerializer.Deserialize<LockConfig>(json) ?? new LockConfig();
            }
            catch
            {
                return new LockConfig();
            }
        }

        public void SaveConfig(LockConfig config)
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(configFilePath, json);
        }

        public void SavePassword(string password)
        {
            // Generate salt
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            
            // Hash the password with salt
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                // Combine salt and password before hashing
                byte[] saltedPassword = new byte[salt.Length + passwordBytes.Length];
                Buffer.BlockCopy(salt, 0, saltedPassword, 0, salt.Length);
                Buffer.BlockCopy(passwordBytes, 0, saltedPassword, salt.Length, passwordBytes.Length);
                
                byte[] hashBytes = sha256.ComputeHash(saltedPassword);

                // Combine salt and hash for storage
                byte[] combined = new byte[salt.Length + hashBytes.Length];
                Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
                Buffer.BlockCopy(hashBytes, 0, combined, salt.Length, hashBytes.Length);

                File.WriteAllBytes(passwordFilePath, combined);
            }
        }

        public bool ValidatePassword(string password)
        {
            if (!File.Exists(passwordFilePath))
            {
                return false;
            }

            try
            {
                byte[] stored = File.ReadAllBytes(passwordFilePath);
                
                // Extract salt
                byte[] salt = new byte[16];
                Buffer.BlockCopy(stored, 0, salt, 0, 16);
                
                // Extract stored hash
                byte[] storedHash = new byte[stored.Length - 16];
                Buffer.BlockCopy(stored, 16, storedHash, 0, storedHash.Length);
                
                // Hash the provided password with salt
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                    // Combine salt and password before hashing
                    byte[] saltedPassword = new byte[salt.Length + passwordBytes.Length];
                    Buffer.BlockCopy(salt, 0, saltedPassword, 0, salt.Length);
                    Buffer.BlockCopy(passwordBytes, 0, saltedPassword, salt.Length, passwordBytes.Length);
                    
                    byte[] computedHash = sha256.ComputeHash(saltedPassword);
                    
                    // Compare hashes
                    if (computedHash.Length != storedHash.Length)
                        return false;
                    
                    for (int i = 0; i < computedHash.Length; i++)
                    {
                        if (computedHash[i] != storedHash[i])
                            return false;
                    }
                    
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool IsPasswordSet()
        {
            // For the new server-based model, we treat an existing account
            // email as the indicator that protection is configured.
            var config = LoadConfig();
            return !string.IsNullOrWhiteSpace(config.AccountEmail);
        }

        public void SaveAccountEmail(string email)
        {
            var config = LoadConfig();
            config.AccountEmail = email ?? string.Empty;
            SaveConfig(config);
        }

        public void ClearAccountCredentials()
        {
            var config = LoadConfig();
            config.AccountEmail = string.Empty;
            config.EncryptedAccountPassword = string.Empty;
            SaveConfig(config);
        }

        public string GetAccountEmail()
        {
            var config = LoadConfig();
            return config.AccountEmail ?? string.Empty;
        }

        public void SaveAccountCredentials(string email, string password)
        {
            var config = LoadConfig();
            config.AccountEmail = email ?? string.Empty;

            if (!string.IsNullOrEmpty(password))
            {
                try
                {
                    byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                    byte[] protectedBytes = ProtectedData.Protect(passwordBytes, null, DataProtectionScope.CurrentUser);
                    config.EncryptedAccountPassword = Convert.ToBase64String(protectedBytes);
                }
                catch
                {
                    // If encryption fails, don't block saving the email.
                }
            }

            SaveConfig(config);
        }

        public bool TryGetAccountPassword(out string password)
        {
            password = string.Empty;

            try
            {
                var config = LoadConfig();
                if (string.IsNullOrWhiteSpace(config.EncryptedAccountPassword))
                {
                    return false;
                }

                byte[] protectedBytes = Convert.FromBase64String(config.EncryptedAccountPassword);
                byte[] passwordBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                password = Encoding.UTF8.GetString(passwordBytes);
                return true;
            }
            catch
            {
                password = string.Empty;
                return false;
            }
        }

        // Unexpected shutdown detection methods
        public void SetRunningFlag()
        {
            try
            {
                File.WriteAllText(shutdownFlagPath, DateTime.Now.ToString("O"));
            }
            catch { }
        }

        public void ClearRunningFlag()
        {
            try
            {
                if (File.Exists(shutdownFlagPath))
                {
                    File.Delete(shutdownFlagPath);
                }
            }
            catch { }
        }

        public bool WasUnexpectedShutdown()
        {
            return File.Exists(shutdownFlagPath);
        }

        public DateTime? GetLastRunTime()
        {
            try
            {
                if (File.Exists(shutdownFlagPath))
                {
                    string content = File.ReadAllText(shutdownFlagPath);
                    return DateTime.Parse(content);
                }
            }
            catch { }
            return null;
        }
    }
}

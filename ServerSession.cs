using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PCLockScreen
{
    /// <summary>
    /// Central helper for managing the authenticated session with the server
    /// and retrieving server-managed schedules.
    /// </summary>
    public static class ServerSession
    {
        private const string DefaultBaseUrl = "https://dashboard.lockpc.co.uk";

        private static ServerClient _client;
        private static string _currentEmail;

        // Resolve base URL at runtime: prefer configured `ServerBaseUrl` from
        // the persisted config (allows changing endpoint without rebuilding),
        // fall back to the compile-time default.
        private static string ResolveBaseUrl()
        {
            try
            {
                var cfgMgr = new ConfigManager();
                var cfg = cfgMgr.LoadConfig();
                if (!string.IsNullOrWhiteSpace(cfg.ServerBaseUrl))
                {
                    return cfg.ServerBaseUrl.Trim();
                }
            }
            catch { }

            return DefaultBaseUrl;
        }

        // Backwards-compatible public accessor used by other code paths
        // that previously referenced `ServerSession.BaseUrl`.
        public static string BaseUrl => ResolveBaseUrl();

        public static string CurrentEmail => _currentEmail;
        public static bool IsLoggedIn => !string.IsNullOrWhiteSpace(_currentEmail);
        public static async Task<bool> EnsureLoggedInAsync(ConfigManager configManager)
        {
            if (IsLoggedIn)
            {
                return true;
            }

            if (configManager == null)
            {
                return false;
            }

            string email = configManager.GetAccountEmail();
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            if (!configManager.TryGetAccountPassword(out string password) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            return await LoginAsync(email, password).ConfigureAwait(false);
        }

        private static ServerClient GetClient()
        {
            if (_client == null)
            {
                var baseUrl = ResolveBaseUrl();
                _client = new ServerClient(baseUrl);
            }
            return _client;
        }

        public static async Task<bool> RegisterAsync(string email, string password)
        {
            try
            {
                var client = GetClient();
                var response = await client.RegisterAsync(email, password).ConfigureAwait(false);
                return IsAuthSuccess(response);
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> RegisterPcAsync(string pcId, string pcName, string localIp)
        {
            if (string.IsNullOrWhiteSpace(pcId))
            {
                return false;
            }

            try
            {
                var client = GetClient();
                var response = await client.RegisterPcAsync(pcId, pcName ?? string.Empty, localIp ?? string.Empty).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> LoginAsync(string email, string password)
        {
            try
            {
                var client = GetClient();
                var response = await client.LoginAsync(email, password).ConfigureAwait(false);

                // The server returns HTML; invalid logins include an
                // "Invalid credentials" message in the body. We must
                // inspect the response content and not rely solely on
                // status codes.
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!string.IsNullOrEmpty(body) &&
                    body.IndexOf("Invalid credentials", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }

                if (IsAuthSuccess(response))
                {
                    _currentEmail = email;
                    return true;
                }
                return false;
            }
            catch
            {
                // propagate connectivity problems by throwing so the UI can show a helpful message
                throw;
            }
        }

        public static async Task<bool> PingServerAsync()
        {
            try
            {
                var client = GetClient();
                var resp = await client.PingAsync().ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> RegisterAndLoginAsync(string email, string password)
        {
            // Ignore register failure details; login will still validate credentials
            await RegisterAsync(email, password).ConfigureAwait(false);
            return await LoginAsync(email, password).ConfigureAwait(false);
        }

        /// <summary>
        /// Validate a password for the current user. If no runtime user is set,
        /// a fallback email (typically from persisted config) can be supplied.
        /// </summary>
        public static async Task<bool> ValidateCurrentUserPasswordAsync(string password, string fallbackEmail = null)
        {
            string email = _currentEmail;
            if (string.IsNullOrWhiteSpace(email))
            {
                email = fallbackEmail;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            // We intentionally re-use LoginAsync here so that the server decides
            // whether the credentials are still valid.
            return await LoginAsync(email, password).ConfigureAwait(false);
        }

        private static bool IsAuthSuccess(HttpResponseMessage response)
        {
            if (response == null)
            {
                return false;
            }

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // The server may respond with redirects (302) on success
            return response.StatusCode == HttpStatusCode.Found;
        }

        private class ServerTimeBlockDto
        {
            // Matches server JSON: { id, from, to, days: ["sun"] }
            [JsonPropertyName("from")]
            public string From { get; set; }

            [JsonPropertyName("to")]
            public string To { get; set; }

            public List<string> Days { get; set; }
        }

        public class ServerReminderDto
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Time { get; set; }
            public List<string> Days { get; set; }
            public bool Persistent { get; set; }
        }

        public class Reminder
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Time { get; set; }
            public List<DayOfWeek> Days { get; set; }
            public bool Persistent { get; set; }
        }

        /// <summary>
        /// Fetch the lock schedule from the server. Expects a JSON array of
        /// objects like: [{ "startTime": "22:00", "endTime": "08:00", "days": ["Monday", ...] }]
        /// </summary>
        public static async Task<List<TimeBlock>> GetScheduleAsync()
        {
            string json;
            try
            {
                var client = GetClient();
                json = await client.GetBlockPeriodsJsonAsync().ConfigureAwait(false);
            }
            catch
            {
                return new List<TimeBlock>();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<TimeBlock>();
            }

            List<ServerTimeBlockDto> serverBlocks;
            try
            {
                serverBlocks = JsonSerializer.Deserialize<List<ServerTimeBlockDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<ServerTimeBlockDto>();
            }
            catch
            {
                // If parsing fails, treat as no schedule
                return new List<TimeBlock>();
            }

            var result = new List<TimeBlock>();

            foreach (var sb in serverBlocks)
            {
                if (sb == null)
                    continue;

                var block = new TimeBlock
                {
                    StartTime = string.IsNullOrWhiteSpace(sb.From) ? "22:00" : sb.From,
                    EndTime = string.IsNullOrWhiteSpace(sb.To) ? "08:00" : sb.To,
                    Days = new List<DayOfWeek>()
                };

                if (sb.Days != null)
                {
                    foreach (var d in sb.Days)
                    {
                        if (string.IsNullOrWhiteSpace(d))
                            continue;

                        // Support both numeric (0-6) and named days
                        if (int.TryParse(d, out int dayIndex) && dayIndex >= 0 && dayIndex <= 6)
                        {
                            block.Days.Add((DayOfWeek)dayIndex);
                        }
                        else
                        {
                            var normalized = d.Trim().ToLowerInvariant();

                            switch (normalized)
                            {
                                case "mon":
                                case "monday":
                                    block.Days.Add(DayOfWeek.Monday);
                                    break;
                                case "tue":
                                case "tues":
                                case "tuesday":
                                    block.Days.Add(DayOfWeek.Tuesday);
                                    break;
                                case "wed":
                                case "weds":
                                case "wednesday":
                                    block.Days.Add(DayOfWeek.Wednesday);
                                    break;
                                case "thu":
                                case "thur":
                                case "thurs":
                                case "thursday":
                                    block.Days.Add(DayOfWeek.Thursday);
                                    break;
                                case "fri":
                                case "friday":
                                    block.Days.Add(DayOfWeek.Friday);
                                    break;
                                case "sat":
                                case "saturday":
                                    block.Days.Add(DayOfWeek.Saturday);
                                    break;
                                case "sun":
                                case "sunday":
                                    block.Days.Add(DayOfWeek.Sunday);
                                    break;
                                default:
                                    // As a fallback, try parsing to DayOfWeek by name
                                    if (Enum.TryParse(typeof(DayOfWeek), d, true, out var dayEnum))
                                    {
                                        block.Days.Add((DayOfWeek)dayEnum);
                                    }
                                    break;
                            }
                        }
                    }
                }

                if (block.Days.Count == 0)
                {
                    // If server didn't specify days, assume every day
                    block.Days.AddRange(new[]
                    {
                        DayOfWeek.Monday,
                        DayOfWeek.Tuesday,
                        DayOfWeek.Wednesday,
                        DayOfWeek.Thursday,
                        DayOfWeek.Friday,
                        DayOfWeek.Saturday,
                        DayOfWeek.Sunday
                    });
                }

                result.Add(block);
            }

            return result;
        }

        /// <summary>
        /// Fetch reminders from the server. Expects a JSON array of
        /// objects like: [{ "id": 1, "title": "Take a shower", "time": "18:00", "days": ["mon", ...] }]
        /// </summary>
        public static async Task<List<Reminder>> GetRemindersAsync()
        {
            string json;
            try
            {
                var client = GetClient();
                json = await client.GetRemindersJsonAsync().ConfigureAwait(false);
            }
            catch
            {
                return new List<Reminder>();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<Reminder>();
            }

            List<ServerReminderDto> serverReminders;
            try
            {
                serverReminders = JsonSerializer.Deserialize<List<ServerReminderDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<ServerReminderDto>();
            }
            catch
            {
                return new List<Reminder>();
            }

            var result = new List<Reminder>();

            foreach (var sr in serverReminders)
            {
                if (sr == null || string.IsNullOrWhiteSpace(sr.Title))
                    continue;

                var reminder = new Reminder
                {
                    Id = sr.Id,
                    Title = sr.Title,
                    Time = sr.Time ?? "12:00",
                    Days = new List<DayOfWeek>(),
                    Persistent = sr.Persistent
                };

                if (sr.Days != null)
                {
                    foreach (var d in sr.Days)
                    {
                        if (string.IsNullOrWhiteSpace(d))
                            continue;

                        var normalized = d.Trim().ToLowerInvariant();

                        switch (normalized)
                        {
                            case "mon":
                            case "monday":
                                reminder.Days.Add(DayOfWeek.Monday);
                                break;
                            case "tue":
                            case "tues":
                            case "tuesday":
                                reminder.Days.Add(DayOfWeek.Tuesday);
                                break;
                            case "wed":
                            case "weds":
                            case "wednesday":
                                reminder.Days.Add(DayOfWeek.Wednesday);
                                break;
                            case "thu":
                            case "thur":
                            case "thurs":
                            case "thursday":
                                reminder.Days.Add(DayOfWeek.Thursday);
                                break;
                            case "fri":
                            case "friday":
                                reminder.Days.Add(DayOfWeek.Friday);
                                break;
                            case "sat":
                            case "saturday":
                                reminder.Days.Add(DayOfWeek.Saturday);
                                break;
                            case "sun":
                            case "sunday":
                                reminder.Days.Add(DayOfWeek.Sunday);
                                break;
                        }
                    }
                }

                result.Add(reminder);
            }

            return result;
        }

        public static void Logout()
        {
            try
            {
                // Request server-side logout so session cookies are cleared
                if (_client != null)
                {
                    try { var _ = _client.LogoutAsync().ConfigureAwait(false).GetAwaiter().GetResult(); } catch { }
                }
            }
            catch { }

            _currentEmail = null;
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }
    }
}

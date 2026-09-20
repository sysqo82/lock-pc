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

        private static readonly List<DayOfWeek> AllSevenDays = new List<DayOfWeek>
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };

        private static readonly List<DayOfWeek> WeekdaysList = new List<DayOfWeek>
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday
        };

        private static readonly List<DayOfWeek> WeekendsList = new List<DayOfWeek>
        {
            DayOfWeek.Saturday, DayOfWeek.Sunday
        };

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

        public static async Task<bool> EnsureLoggedInAsync(ConfigManager configManager = null)
        {
            if (IsLoggedIn)
            {
                return true;
            }

            configManager ??= new ConfigManager();

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

        public static List<DayOfWeek> ParseDays(IEnumerable<string> rawDays)
        {
            var days = new HashSet<DayOfWeek>();
            if (rawDays == null)
            {
                return new List<DayOfWeek>(AllSevenDays);
            }

            foreach (var raw in rawDays)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var normalized = raw.Trim().ToLowerInvariant();

                switch (normalized)
                {
                    case "everyday":
                    case "every day":
                    case "every_day":
                    case "daily":
                    case "all":
                    case "all days":
                    case "all_days":
                    case "always":
                        foreach (var d in AllSevenDays) days.Add(d);
                        continue;

                    case "weekdays":
                    case "weekday":
                    case "workdays":
                    case "workday":
                    case "mon-fri":
                    case "mon - fri":
                        foreach (var d in WeekdaysList) days.Add(d);
                        continue;

                    case "weekends":
                    case "weekend":
                    case "sat-sun":
                    case "sat - sun":
                        foreach (var d in WeekendsList) days.Add(d);
                        continue;
                }

                switch (normalized)
                {
                    case "mon":
                    case "monday":
                        days.Add(DayOfWeek.Monday);
                        break;
                    case "tue":
                    case "tues":
                    case "tuesday":
                        days.Add(DayOfWeek.Tuesday);
                        break;
                    case "wed":
                    case "weds":
                    case "wednesday":
                        days.Add(DayOfWeek.Wednesday);
                        break;
                    case "thu":
                    case "thur":
                    case "thurs":
                    case "thursday":
                        days.Add(DayOfWeek.Thursday);
                        break;
                    case "fri":
                    case "friday":
                        days.Add(DayOfWeek.Friday);
                        break;
                    case "sat":
                    case "saturday":
                        days.Add(DayOfWeek.Saturday);
                        break;
                    case "sun":
                    case "sunday":
                        days.Add(DayOfWeek.Sunday);
                        break;
                    default:
                        if (int.TryParse(normalized, out int dayIndex))
                        {
                            if (dayIndex >= 0 && dayIndex <= 6)
                            {
                                days.Add((DayOfWeek)dayIndex);
                            }
                            else if (dayIndex == 7)
                            {
                                days.Add(DayOfWeek.Sunday);
                            }
                        }
                        else if (Enum.TryParse<DayOfWeek>(raw.Trim(), true, out var dayEnum))
                        {
                            days.Add(dayEnum);
                        }
                        break;
                }
            }

            if (days.Count == 0)
            {
                return new List<DayOfWeek>();
            }

            return new List<DayOfWeek>(days);
        }

        private class ServerTimeBlockDto
        {
            [JsonPropertyName("from")]
            public string From { get; set; }

            [JsonPropertyName("startTime")]
            public string StartTime { get; set; }

            [JsonPropertyName("start_time")]
            public string StartTimeSnake { get; set; }

            [JsonPropertyName("start")]
            public string Start { get; set; }

            [JsonPropertyName("to")]
            public string To { get; set; }

            [JsonPropertyName("endTime")]
            public string EndTime { get; set; }

            [JsonPropertyName("end_time")]
            public string EndTimeSnake { get; set; }

            [JsonPropertyName("end")]
            public string End { get; set; }

            [JsonPropertyName("day")]
            public JsonElement DayElement { get; set; }

            [JsonPropertyName("days")]
            public JsonElement DaysElement { get; set; }

            [JsonPropertyName("dayList")]
            public JsonElement DayListElement { get; set; }

            [JsonPropertyName("day_list")]
            public JsonElement DayListSnakeElement { get; set; }

            [JsonPropertyName("daysOfWeek")]
            public JsonElement DaysOfWeekElement { get; set; }

            [JsonPropertyName("days_of_week")]
            public JsonElement DaysOfWeekSnakeElement { get; set; }

            [JsonPropertyName("selectedDays")]
            public JsonElement SelectedDaysElement { get; set; }

            [JsonPropertyName("selected_days")]
            public JsonElement SelectedDaysSnakeElement { get; set; }

            public string EffectiveFrom =>
                !string.IsNullOrWhiteSpace(From) ? From :
                !string.IsNullOrWhiteSpace(StartTime) ? StartTime :
                !string.IsNullOrWhiteSpace(StartTimeSnake) ? StartTimeSnake :
                !string.IsNullOrWhiteSpace(Start) ? Start : null;

            public string EffectiveTo =>
                !string.IsNullOrWhiteSpace(To) ? To :
                !string.IsNullOrWhiteSpace(EndTime) ? EndTime :
                !string.IsNullOrWhiteSpace(EndTimeSnake) ? EndTimeSnake :
                !string.IsNullOrWhiteSpace(End) ? End : null;

            public List<string> GetDaysList()
            {
                var result = new List<string>();

                JsonElement[] candidateElems = new[]
                {
                    DaysElement, DayElement, DayListElement, DayListSnakeElement,
                    DaysOfWeekElement, DaysOfWeekSnakeElement, SelectedDaysElement, SelectedDaysSnakeElement
                };

                JsonElement elem = default;
                bool found = false;

                foreach (var candidate in candidateElems)
                {
                    if (candidate.ValueKind != JsonValueKind.Undefined && candidate.ValueKind != JsonValueKind.Null)
                    {
                        elem = candidate;
                        found = true;
                        break;
                    }
                }

                if (!found) return result;

                if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var val = item.GetString();
                            if (!string.IsNullOrWhiteSpace(val))
                                result.Add(val);
                        }
                        else if (item.ValueKind == JsonValueKind.Number)
                        {
                            result.Add(item.GetInt32().ToString());
                        }
                    }
                }
                else if (elem.ValueKind == JsonValueKind.String)
                {
                    var str = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(str))
                    {
                        var parts = str.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        result.AddRange(parts);
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Number)
                {
                    result.Add(elem.GetInt32().ToString());
                }

                return result;
            }
        }

        public class ServerReminderDto
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Time { get; set; }

            [JsonPropertyName("days")]
            public JsonElement DaysElement { get; set; }

            [JsonPropertyName("dayList")]
            public JsonElement DayListElement { get; set; }

            public bool Persistent { get; set; }

            public List<string> GetDaysList()
            {
                var result = new List<string>();
                var elem = DaysElement.ValueKind != JsonValueKind.Undefined && DaysElement.ValueKind != JsonValueKind.Null
                    ? DaysElement
                    : DayListElement;

                if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var val = item.GetString();
                            if (!string.IsNullOrWhiteSpace(val))
                                result.Add(val);
                        }
                        else if (item.ValueKind == JsonValueKind.Number)
                        {
                            result.Add(item.GetInt32().ToString());
                        }
                    }
                }
                else if (elem.ValueKind == JsonValueKind.String)
                {
                    var str = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(str))
                    {
                        var parts = str.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        result.AddRange(parts);
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Number)
                {
                    result.Add(elem.GetInt32().ToString());
                }

                return result;
            }
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
        /// Fetch the lock schedule from the server. Expects a JSON array or wrapped object of time blocks.
        /// Queries both general and per-PC endpoints using the current PcId.
        /// Returns null on server/network errors to preserve local schedule configuration.
        /// </summary>
        public static async Task<List<TimeBlock>> GetScheduleAsync()
        {
            try
            {
                await EnsureLoggedInAsync().ConfigureAwait(false);
            }
            catch { }

            var cfgMgr = new ConfigManager();
            string pcId = null;
            try
            {
                pcId = cfgMgr.GetOrCreatePcId();
            }
            catch { }

            List<string> jsonList = null;
            try
            {
                var client = GetClient();
                jsonList = await client.GetBlockPeriodsJsonListAsync(pcId).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                InvalidateSession();
                try
                {
                    bool relogged = await EnsureLoggedInAsync().ConfigureAwait(false);
                    if (relogged)
                    {
                        var client = GetClient();
                        jsonList = await client.GetBlockPeriodsJsonListAsync(pcId).ConfigureAwait(false);
                    }
                }
                catch
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }

            if (jsonList == null || jsonList.Count == 0)
            {
                return null;
            }

            var allServerBlocks = new List<ServerTimeBlockDto>();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            foreach (var json in jsonList)
            {
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        var blocks = JsonSerializer.Deserialize<List<ServerTimeBlockDto>>(json, options);
                        if (blocks != null) allServerBlocks.AddRange(blocks);
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        string[] arrayPropNames = new[] { "blockPeriods", "block_periods", "blocks", "data", "schedule", "schedules", "periods", "timeBlocks", "time_blocks", "items" };
                        bool foundArray = false;
                        foreach (var propName in arrayPropNames)
                        {
                            if (root.TryGetProperty(propName, out var arrayElem) && arrayElem.ValueKind == JsonValueKind.Array)
                            {
                                var blocks = JsonSerializer.Deserialize<List<ServerTimeBlockDto>>(arrayElem.GetRawText(), options);
                                if (blocks != null) allServerBlocks.AddRange(blocks);
                                foundArray = true;
                                break;
                            }
                        }

                        if (!foundArray)
                        {
                            var singleBlock = JsonSerializer.Deserialize<ServerTimeBlockDto>(json, options);
                            if (singleBlock != null && (!string.IsNullOrWhiteSpace(singleBlock.EffectiveFrom) || !string.IsNullOrWhiteSpace(singleBlock.EffectiveTo)))
                            {
                                allServerBlocks.Add(singleBlock);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore JSON parse errors for an individual payload
                }
            }

            var result = new List<TimeBlock>();
            var blockKeys = new HashSet<string>();

            foreach (var sb in allServerBlocks)
            {
                if (sb == null)
                    continue;

                var from = sb.EffectiveFrom;
                var to = sb.EffectiveTo;

                var parsedDays = ParseDays(sb.GetDaysList());
                var startTimeStr = string.IsNullOrWhiteSpace(from) ? "22:00" : from.Trim();
                var endTimeStr = string.IsNullOrWhiteSpace(to) ? "08:00" : to.Trim();

                var daysKey = string.Join(",", parsedDays);
                var key = $"{startTimeStr}|{endTimeStr}|{daysKey}";

                if (blockKeys.Contains(key))
                    continue;

                blockKeys.Add(key);

                var block = new TimeBlock
                {
                    StartTime = startTimeStr,
                    EndTime = endTimeStr,
                    Days = parsedDays
                };

                result.Add(block);
            }

            return result;
        }

        /// <summary>
        /// Fetch reminders from the server. Expects a JSON array or wrapped object of reminders.
        /// Queries both general and per-PC endpoints using the current PcId.
        /// Returns null on server/network errors.
        /// </summary>
        public static async Task<List<Reminder>> GetRemindersAsync()
        {
            try
            {
                await EnsureLoggedInAsync().ConfigureAwait(false);
            }
            catch { }

            var cfgMgr = new ConfigManager();
            string pcId = null;
            try
            {
                pcId = cfgMgr.GetOrCreatePcId();
            }
            catch { }

            List<string> jsonList = null;
            try
            {
                var client = GetClient();
                jsonList = await client.GetRemindersJsonListAsync(pcId).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                InvalidateSession();
                try
                {
                    bool relogged = await EnsureLoggedInAsync().ConfigureAwait(false);
                    if (relogged)
                    {
                        var client = GetClient();
                        jsonList = await client.GetRemindersJsonListAsync(pcId).ConfigureAwait(false);
                    }
                }
                catch
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }

            if (jsonList == null || jsonList.Count == 0)
            {
                return null;
            }

            var allServerReminders = new List<ServerReminderDto>();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            foreach (var json in jsonList)
            {
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        var reminders = JsonSerializer.Deserialize<List<ServerReminderDto>>(json, options);
                        if (reminders != null) allServerReminders.AddRange(reminders);
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        string[] arrayPropNames = new[] { "reminders", "reminderList", "data", "items" };
                        bool foundArray = false;
                        foreach (var propName in arrayPropNames)
                        {
                            if (root.TryGetProperty(propName, out var arrayElem) && arrayElem.ValueKind == JsonValueKind.Array)
                            {
                                var reminders = JsonSerializer.Deserialize<List<ServerReminderDto>>(arrayElem.GetRawText(), options);
                                if (reminders != null) allServerReminders.AddRange(reminders);
                                foundArray = true;
                                break;
                            }
                        }

                        if (!foundArray)
                        {
                            var singleReminder = JsonSerializer.Deserialize<ServerReminderDto>(json, options);
                            if (singleReminder != null && !string.IsNullOrWhiteSpace(singleReminder.Title))
                            {
                                allServerReminders.Add(singleReminder);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore
                }
            }

            var result = new List<Reminder>();
            var reminderKeys = new HashSet<string>();

            foreach (var sr in allServerReminders)
            {
                if (sr == null || string.IsNullOrWhiteSpace(sr.Title))
                    continue;

                var parsedDays = ParseDays(sr.GetDaysList());
                var daysKey = string.Join(",", parsedDays);
                var key = $"{sr.Id}|{sr.Title}|{sr.Time}|{daysKey}";

                if (reminderKeys.Contains(key))
                    continue;

                reminderKeys.Add(key);

                var reminder = new Reminder
                {
                    Id = sr.Id,
                    Title = sr.Title,
                    Time = sr.Time ?? "12:00",
                    Days = parsedDays,
                    Persistent = sr.Persistent
                };

                result.Add(reminder);
            }

            return result;
        }

        /// <summary>
        /// Clears the in-memory session state so the next call to
        /// <see cref="EnsureLoggedInAsync"/> will perform a fresh login.
        /// Called automatically when the server returns 401 (e.g. after a
        /// container restart that wipes the in-memory session store).
        /// </summary>
        private static void InvalidateSession()
        {
            _currentEmail = null;
            if (_client != null)
            {
                try { _client.Dispose(); } catch { }
                _client = null;
            }
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

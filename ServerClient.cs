using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PCLockScreen
{
    /// <summary>
    /// HTTP client wrapper for talking to the remote lock-pc server.
    /// Keeps a CookieContainer so session-based auth works across requests.
    /// </summary>
    public class ServerClient : IDisposable
    {
        private readonly HttpClient _client;
        private readonly CookieContainer _cookies;

        public ServerClient(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("Base URL must be provided", nameof(baseUrl));

            _cookies = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                UseCookies = true
            };

            _client = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl)
            };
        }

        public async Task<HttpResponseMessage> RegisterAsync(string email, string password)
        {
            var data = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("email", email),
                new KeyValuePair<string, string>("password", password)
            });

            return await _client.PostAsync("/register", data).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> LoginAsync(string email, string password)
        {
            var data = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("email", email),
                new KeyValuePair<string, string>("password", password)
            });

            return await _client.PostAsync("/login", data).ConfigureAwait(false);
        }

        /// <summary>
        /// Register this PC with the authenticated user. Requires that the
        /// session cookie from a successful login is already present.
        /// </summary>
        public async Task<HttpResponseMessage> RegisterPcAsync(string id, string name, string localIp)
        {
            var payload = new
            {
                id = id,
                name = name,
                localIp = localIp,
                clientType = "pc_app"
            };

            string json = JsonSerializer.Serialize(payload);

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/register-pc")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            return await _client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<string> GetDashboardHtmlAsync()
        {
            var response = await _client.GetAsync("/dashboard").ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Get the lock schedule from the server as raw JSON payloads.
        /// Supports querying per-PC endpoints, pluralized API paths, and global endpoints.
        /// Throws <see cref="UnauthorizedAccessException"/> when the server returns 401.
        /// </summary>
        public async Task<List<string>> GetBlockPeriodsJsonListAsync(string pcId = null)
        {
            var responses = new List<string>();
            var endpointsToTry = new List<string>();

            if (!string.IsNullOrWhiteSpace(pcId))
            {
                var cleanPcId = Uri.EscapeDataString(pcId.Trim());
                endpointsToTry.Add($"/api/block-periods?pcId={cleanPcId}&pc_id={cleanPcId}&id={cleanPcId}");
                endpointsToTry.Add($"/api/block-period?pcId={cleanPcId}&pc_id={cleanPcId}&id={cleanPcId}");
                endpointsToTry.Add($"/api/schedules?pcId={cleanPcId}&pc_id={cleanPcId}&id={cleanPcId}");
                endpointsToTry.Add($"/api/schedule?pcId={cleanPcId}&pc_id={cleanPcId}&id={cleanPcId}");
                endpointsToTry.Add($"/api/pc/{cleanPcId}/block-periods");
                endpointsToTry.Add($"/api/pc/{cleanPcId}/block-period");
                endpointsToTry.Add($"/api/pc/{cleanPcId}/schedules");
                endpointsToTry.Add($"/api/pc/{cleanPcId}/schedule");
            }

            endpointsToTry.Add("/api/block-periods");
            endpointsToTry.Add("/api/block-period");
            endpointsToTry.Add("/api/block_periods");
            endpointsToTry.Add("/api/block_period");
            endpointsToTry.Add("/api/schedules");
            endpointsToTry.Add("/api/schedule");
            endpointsToTry.Add("/api/time-blocks");
            endpointsToTry.Add("/api/time_blocks");
            endpointsToTry.Add("/api/timeblocks");
            endpointsToTry.Add("/api/timeblock");

            bool hadUnauthorized = false;

            foreach (var path in endpointsToTry)
            {
                try
                {
                    var response = await _client.GetAsync(path).ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        hadUnauthorized = true;
                        continue;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(json) && json.Length > 2)
                        {
                            responses.Add(json);
                        }
                    }
                }
                catch
                {
                    // Ignore individual endpoint failure
                }
            }

            if (responses.Count == 0 && hadUnauthorized)
            {
                throw new UnauthorizedAccessException("Server session expired or invalid.");
            }

            return responses;
        }

        public async Task<string> GetBlockPeriodsJsonAsync(string pcId = null)
        {
            var list = await GetBlockPeriodsJsonListAsync(pcId).ConfigureAwait(false);
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// Get reminders from the server as raw JSON payloads.
        /// Supports querying per-PC endpoints, pluralized API paths, and global endpoints.
        /// Throws <see cref="UnauthorizedAccessException"/> when the server returns 401.
        /// </summary>
        public async Task<List<string>> GetRemindersJsonListAsync(string pcId = null)
        {
            var responses = new List<string>();
            var endpointsToTry = new List<string>();

            if (!string.IsNullOrWhiteSpace(pcId))
            {
                var cleanPcId = Uri.EscapeDataString(pcId.Trim());
                endpointsToTry.Add($"/api/reminders?pcId={cleanPcId}&pc_id={cleanPcId}&id={cleanPcId}");
                endpointsToTry.Add($"/api/reminder?pcId={cleanPcId}&pc_id={cleanPcId}&id={cleanPcId}");
                endpointsToTry.Add($"/api/pc/{cleanPcId}/reminders");
                endpointsToTry.Add($"/api/pc/{cleanPcId}/reminder");
            }

            endpointsToTry.Add("/api/reminders");
            endpointsToTry.Add("/api/reminder");
            endpointsToTry.Add("/api/reminder_list");
            endpointsToTry.Add("/api/reminder-list");

            bool hadUnauthorized = false;

            foreach (var path in endpointsToTry)
            {
                try
                {
                    var response = await _client.GetAsync(path).ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        hadUnauthorized = true;
                        continue;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(json) && json.Length > 2)
                        {
                            responses.Add(json);
                        }
                    }
                }
                catch
                {
                    // Ignore individual endpoint failure
                }
            }

            if (responses.Count == 0 && hadUnauthorized)
            {
                throw new UnauthorizedAccessException("Server session expired or invalid.");
            }

            return responses;
        }

        public async Task<string> GetRemindersJsonAsync(string pcId = null)
        {
            var list = await GetRemindersJsonListAsync(pcId).ConfigureAwait(false);
            return list.Count > 0 ? list[0] : null;
        }

        public async Task<HttpResponseMessage> LogoutAsync()
        {
            try
            {
                return await _client.GetAsync("/logout").ConfigureAwait(false);
            }
            catch
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
            }
        }

        public async Task<HttpResponseMessage> PingAsync()
        {
            try
            {
                return await _client.GetAsync("/").ConfigureAwait(false);
            }
            catch
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}

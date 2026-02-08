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
        /// Get the lock schedule from the server as raw JSON.
        /// Expected path is /api/block-period and should return a JSON array
        /// of time block definitions.
        /// </summary>
        public async Task<string> GetBlockPeriodsJsonAsync()
        {
            var response = await _client.GetAsync("/api/block-period").ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Get the reminders from the server as raw JSON.
        /// Expected path is /api/reminder and should return a JSON array
        /// of reminder definitions.
        /// </summary>
        public async Task<string> GetRemindersJsonAsync()
        {
            var response = await _client.GetAsync("/api/reminder").ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}

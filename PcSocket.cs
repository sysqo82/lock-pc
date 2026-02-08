using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace PCLockScreen
{
    /// <summary>
    /// Socket.IO client used by the PC to register with the server
    /// and receive remote commands (e.g. lock).
    /// </summary>
    public class PcSocket
    {
        private readonly SocketIOClient.SocketIO _socket;
        private readonly string _pcId;
        private readonly string _pcName;
        private readonly string _serverUrl;
        private readonly Action<string> _commandHandler;
        private readonly Action _scheduleUpdateHandler;
        private readonly Action _reminderUpdateHandler;
        private readonly Func<string> _statusProvider;

        public PcSocket(string serverBaseUrl, string pcId, string pcName, Action<string> commandHandler = null, Action scheduleUpdateHandler = null, Func<string> statusProvider = null, Action reminderUpdateHandler = null)
        {
            if (string.IsNullOrWhiteSpace(serverBaseUrl))
                throw new ArgumentException("Server base URL must be provided", nameof(serverBaseUrl));
            if (string.IsNullOrWhiteSpace(pcId))
                throw new ArgumentException("PC id must be provided", nameof(pcId));

            _pcId = pcId;
            _pcName = string.IsNullOrWhiteSpace(pcName) ? Environment.MachineName : pcName;
            _serverUrl = serverBaseUrl;
            _commandHandler = commandHandler;
            _scheduleUpdateHandler = scheduleUpdateHandler;
            _reminderUpdateHandler = reminderUpdateHandler;
            _statusProvider = statusProvider;

            _socket = new SocketIOClient.SocketIO(serverBaseUrl, new SocketIOClient.SocketIOOptions
            {
                EIO = SocketIO.Core.EngineIO.V4, // Engine.IO protocol version 4
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket | SocketIOClient.Transport.TransportProtocol.Polling,
                ConnectionTimeout = TimeSpan.FromSeconds(30),
                ReconnectionDelay = 5000,
                ReconnectionDelayMax = 30000
            });

            _socket.OnConnected += async (sender, args) =>
            {
                try
                {
                    Logger.Log($"Socket connected for PC {_pcId} ({_pcName}), Socket.IO ID: {_socket.Id}");

                    // Directly emit register_pc here (don't call RegisterPcWithSocketAsync to avoid recursion)
                    try
                    {
                        var localIp = GetPreferredLocalIPv4();
                        Logger.Log($"Emitting register_pc for {_pcId} with IP {localIp}");
                        await _socket.EmitAsync("register_pc", new { id = _pcId, name = _pcName, clientType = "pc_app", localIp = localIp }).ConfigureAwait(false);
                        Logger.Log($"Sent register_pc for {_pcId}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to emit register_pc on connect", ex);
                    }

                    // Send initial status even if the provider returns null so the
                    // server updates lastStatusAt and probes can succeed.
                    try
                    {
                        var status = _statusProvider?.Invoke();
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            status = "Unknown";
                            Logger.Log("Status provider returned null/empty on connect — sending Unknown");
                        }
                        await SendStatusAsync(status).ConfigureAwait(false);
                        Logger.Log($"Sent initial status: {status}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Status provider invocation or status send failed on connect", ex);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error handling socket connected event", ex);
                }
            };

            _socket.On("command", response =>
            {
                try
                {
                    var json = response.GetValue<string>();
                    Logger.Log($"Received command payload: {json}");
                    if (string.IsNullOrWhiteSpace(json))
                        return;

                    using var doc = JsonDocument.Parse(json);
                    if (!doc.RootElement.TryGetProperty("action", out var actionElement))
                        return;

                    var action = actionElement.GetString();
                    if (!string.IsNullOrWhiteSpace(action))
                    {
                        Logger.Log($"Invoking command handler: {action}");
                        _commandHandler?.Invoke(action);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error processing command event", ex);
                }
            });

            _socket.On("schedule_update", response =>
            {
                try
                {
                    Logger.Log("Received schedule_update event from server");
                    _scheduleUpdateHandler?.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error invoking schedule update handler", ex);
                }
            });

            _socket.On("reminder_update", response =>
            {
                try
                {
                    Logger.Log("Received reminder_update event from server");
                    _reminderUpdateHandler?.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error invoking reminder update handler", ex);
                }
            });

            // Respond to server probe/status_request by returning current status
            _socket.On("status_request", async response =>
            {
                try
                {
                    Logger.Log("Received status_request from server");
                    string probeId = null;
                    try
                    {
                        var je = response.GetValue<JsonElement>();
                        if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty("probeId", out var p2)) probeId = p2.GetString();
                    }
                    catch
                    {
                        try
                        {
                            var json = response.GetValue<string>();
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("probeId", out var p)) probeId = p.GetString();
                        }
                        catch { }
                    }

                    var status = _statusProvider?.Invoke() ?? "Unknown";
                    // Fire-and-forget — update server mapping via pc_status event
                    _ = SendStatusAsync(status);

                    // If the server provided a probeId, reply directly so probes get immediate answers
                    if (!string.IsNullOrWhiteSpace(probeId))
                    {
                        try
                        {
                            var last = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            await _socket.EmitAsync("status_reply", new { probeId = probeId, status = status, lastStatusAt = last }).ConfigureAwait(false);
                            Logger.Log($"Sent status_reply for probe {probeId} => {status}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to emit status_reply", ex);
                        }
                    }
                    else
                    {
                        Logger.Log($"Replied to status_request with {status} (no probeId)");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error handling status_request", ex);
                }
            });

            _socket.OnDisconnected += (sender, args) =>
            {
                try
                {
                    Logger.Log($"Socket disconnected for PC {_pcId} ({_pcName}). Reason: {args}");
                    // Rely on the Socket.IO client library's automatic reconnect logic
                    // to avoid racing duplicate connection attempts.
                }
                catch { }
            };

            _socket.OnError += (sender, args) =>
            {
                try
                {
                    Logger.Log($"Socket error for PC {_pcId}: {args}");
                }
                catch { }
            };

            _socket.OnReconnectAttempt += (sender, attemptNumber) =>
            {
                try
                {
                    Logger.Log($"Socket reconnect attempt #{attemptNumber} for PC {_pcId}");
                }
                catch { }
            };

            _socket.OnReconnectFailed += (sender, args) =>
            {
                try
                {
                    Logger.Log($"Socket reconnect failed for PC {_pcId}");
                }
                catch { }
            };
        }

        public async Task ConnectAsync()
        {
            try
            {
                if (_socket.Connected)
                {
                    Logger.Log($"ConnectAsync: already connected (Socket.IO ID: {_socket.Id})");
                    return;
                }

                Logger.Log($"ConnectAsync: initiating connection for {_pcId} to {_serverUrl}...");
                
                var connectTask = _socket.ConnectAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));
                
                var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
                
                if (completedTask == timeoutTask)
                {
                    Logger.Log($"ConnectAsync: connection attempt timed out after 15 seconds. Connected: {_socket.Connected}");
                    throw new TimeoutException("Socket.IO connection timed out after 15 seconds");
                }
                
                await connectTask; // Propagate any exception
                Logger.Log($"ConnectAsync: connection completed for {_pcId}. Connected: {_socket.Connected}, ID: {_socket.Id}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"ConnectAsync failed for {_pcId}", ex);
                throw;
            }
        }

        public Task DisconnectAsync() => _socket.DisconnectAsync();

        /// <summary>
        /// Explicitly re-send the register_pc event over the existing socket
        /// connection. This can be used after login to ensure the server
        /// associates the connected socket with the authenticated user.
        /// </summary>
        public async Task RegisterPcWithSocketAsync()
        {
            try
            {
                if (!_socket.Connected)
                {
                    Logger.Log($"RegisterPcWithSocketAsync: socket not connected for {_pcId}, attempting connect...");
                    await _socket.ConnectAsync().ConfigureAwait(false);
                    // Give the OnConnected handler a moment to fire and complete registration
                    await Task.Delay(500).ConfigureAwait(false);
                    return;
                }

                Logger.Log($"Emitting register_pc for {_pcId} (socket connected: {_socket.Connected}, id: {_socket.Id})");
                var localIp = GetPreferredLocalIPv4();
                await _socket.EmitAsync("register_pc", new { id = _pcId, name = _pcName, clientType = "pc_app", localIp = localIp }).ConfigureAwait(false);
                Logger.Log($"register_pc emitted successfully for {_pcId}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"RegisterPcWithSocketAsync failed for {_pcId}", ex);
            }
        }

        private string GetPreferredLocalIPv4()
        {
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    // Skip down, loopback, tunnel, and virtual/adapters we don't want
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    var t = ni.NetworkInterfaceType;
                    if (t == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    if (t == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) continue;

                    var name = ni.Name?.ToLower() ?? string.Empty;
                    var desc = ni.Description?.ToLower() ?? string.Empty;
                    if (name.Contains("vethernet") || name.Contains("docker") || name.Contains("virtual") || desc.Contains("hyper-v") || desc.Contains("vmware")) continue;

                    var props = ni.GetIPProperties();
                    // Prefer interfaces with a default gateway
                    if (props.GatewayAddresses != null && props.GatewayAddresses.Count > 0)
                    {
                        foreach (var ua in props.UnicastAddresses)
                        {
                            if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                return ua.Address.ToString();
                            }
                        }
                    }
                }

                // Fallback: pick any non-loopback IPv4
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    var props = ni.GetIPProperties();
                    foreach (var ua in props.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                            !System.Net.IPAddress.IsLoopback(ua.Address))
                        {
                            return ua.Address.ToString();
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Send the current lock screen status to the server.
        /// </summary>
        /// <param name="status">"Locked" or "Unlocked"</param>
        public async Task SendStatusAsync(string status)
        {
            try
            {
                if (!_socket.Connected)
                {
                    Logger.Log($"SendStatusAsync: socket not connected, cannot send status {status}");
                    return;
                }

                Logger.Log($"Emitting pc_status: {status} (pc: {_pcId}, socket id: {_socket.Id})");
                await _socket.EmitAsync("pc_status", new { id = _pcId, status = status }).ConfigureAwait(false);
                Logger.Log($"pc_status emitted successfully: {status}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"SendStatusAsync failed for status {status}", ex);
            }
        }
    }
}

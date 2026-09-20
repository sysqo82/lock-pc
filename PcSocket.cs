using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace PCLockScreen
{
    /// <summary>
    /// Socket.IO client used by the PC to register with the server
    /// and receive remote commands (e.g. lock, pause, unlock, resume).
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
                    var action = ExtractActionFromResponse(response);
                    Logger.Log($"Received 'command' event: {action}");
                    if (!string.IsNullOrWhiteSpace(action))
                    {
                        _commandHandler?.Invoke(action);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error processing command event", ex);
                }
            });

            _socket.On("pause", response =>
            {
                try
                {
                    var action = ExtractActionFromResponse(response) ?? "pause";
                    Logger.Log($"Received 'pause' event: {action}");
                    _commandHandler?.Invoke(action);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error processing pause event", ex);
                }
            });

            _socket.On("unlock", response =>
            {
                try
                {
                    var action = ExtractActionFromResponse(response) ?? "unlock";
                    Logger.Log($"Received 'unlock' event: {action}");
                    _commandHandler?.Invoke(action);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error processing unlock event", ex);
                }
            });

            _socket.On("freeze", response =>
            {
                try
                {
                    var action = ExtractActionFromResponse(response) ?? "freeze";
                    Logger.Log($"Received 'freeze' event: {action}");
                    _commandHandler?.Invoke(action);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error processing freeze event", ex);
                }
            });

            _socket.On("resume", response =>
            {
                try
                {
                    var action = ExtractActionFromResponse(response) ?? "resume";
                    Logger.Log($"Received 'resume' event: {action}");
                    _commandHandler?.Invoke(action);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error processing resume event", ex);
                }
            });

            _socket.On("unpause", response =>
            {
                try
                {
                    var action = ExtractActionFromResponse(response) ?? "unpause";
                    Logger.Log($"Received 'unpause' event: {action}");
                    _commandHandler?.Invoke(action);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error processing unpause event", ex);
                }
            });

            _socket.On("lock", response =>
            {
                try
                {
                    var action = ExtractActionFromResponse(response) ?? "lock";
                    Logger.Log($"Received 'lock' event: {action}");
                    _commandHandler?.Invoke(action);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error processing lock event", ex);
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
                    _ = SendStatusAsync(status);

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

        private static string ExtractActionFromResponse(SocketIOClient.SocketIOResponse response)
        {
            if (response == null) return null;

            try
            {
                var element = response.GetValue<JsonElement>();
                if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("action", out var prop) && prop.ValueKind == JsonValueKind.String)
                        return prop.GetString();
                    if (element.TryGetProperty("command", out var prop2) && prop2.ValueKind == JsonValueKind.String)
                        return prop2.GetString();
                    if (element.TryGetProperty("type", out var prop3) && prop3.ValueKind == JsonValueKind.String)
                        return prop3.GetString();
                }
                else if (element.ValueKind == JsonValueKind.String)
                {
                    var str = element.GetString();
                    if (!string.IsNullOrWhiteSpace(str))
                    {
                        if (str.TrimStart().StartsWith("{"))
                        {
                            using var doc = JsonDocument.Parse(str);
                            if (doc.RootElement.TryGetProperty("action", out var p1) && p1.ValueKind == JsonValueKind.String) return p1.GetString();
                            if (doc.RootElement.TryGetProperty("command", out var p2) && p2.ValueKind == JsonValueKind.String) return p2.GetString();
                            if (doc.RootElement.TryGetProperty("type", out var p3) && p3.ValueKind == JsonValueKind.String) return p3.GetString();
                        }
                        return str;
                    }
                }
            }
            catch { }

            try
            {
                var rawStr = response.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(rawStr))
                {
                    if (rawStr.TrimStart().StartsWith("{"))
                    {
                        using var doc = JsonDocument.Parse(rawStr);
                        if (doc.RootElement.TryGetProperty("action", out var p1) && p1.ValueKind == JsonValueKind.String) return p1.GetString();
                        if (doc.RootElement.TryGetProperty("command", out var p2) && p2.ValueKind == JsonValueKind.String) return p2.GetString();
                        if (doc.RootElement.TryGetProperty("type", out var p3) && p3.ValueKind == JsonValueKind.String) return p3.GetString();
                    }
                    return rawStr;
                }
            }
            catch { }

            return null;
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

        public async Task RegisterPcWithSocketAsync()
        {
            try
            {
                if (!_socket.Connected)
                {
                    Logger.Log($"RegisterPcWithSocketAsync: socket not connected for {_pcId}, attempting connect...");
                    await _socket.ConnectAsync().ConfigureAwait(false);
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
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    var t = ni.NetworkInterfaceType;
                    if (t == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    if (t == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) continue;

                    var name = ni.Name?.ToLower() ?? string.Empty;
                    var desc = ni.Description?.ToLower() ?? string.Empty;
                    if (name.Contains("vethernet") || name.Contains("docker") || name.Contains("virtual") || desc.Contains("hyper-v") || desc.Contains("vmware")) continue;

                    var props = ni.GetIPProperties();
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

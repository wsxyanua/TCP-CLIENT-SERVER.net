using System.Net;
using System.Net.Sockets;
using System.Text;
using TcpServer.Models;
using Microsoft.Extensions.Hosting;

namespace TcpServer.Services;

public class TcpTimeService : BackgroundService, IDisposable
{
    private TcpListener _listener;
    private readonly ILogger<TcpTimeService> _logger;
    private bool _isRunning;
    private readonly Dictionary<string, ClientInfo> _clients;
    private readonly int _port;
    private readonly Dictionary<string, TcpClient> _clientConnections;
    private readonly Encoding _encoding;
    private int _totalMessages;
    private int _timeRequests;
    private DateTime _startTime;
    private readonly object _stateLock = new object();
    private readonly object _clientLock = new object();
    private readonly object _logLock = new object();
    private readonly Queue<ServerLog> _logs;
    private const int MaxLogs = 1000;

    public bool IsRunning => _isRunning;
    public int TotalMessages => _totalMessages;
    public int TimeRequests => _timeRequests;
    public int ActiveConnections => _clients.Count;
    public TimeSpan Uptime => DateTime.Now - _startTime;
    public DateTime StartTime => _startTime;
    public IReadOnlyDictionary<string, ClientInfo> ConnectedClients => _clients;
    public IReadOnlyList<ServerLog> Logs => _logs.ToList();

    public TcpTimeService(ILogger<TcpTimeService> logger)
    {
        _logger = logger;
        _clientConnections = new Dictionary<string, TcpClient>();
        _clients = new Dictionary<string, ClientInfo>();
        _port = 8888;
        _listener = new TcpListener(IPAddress.Any, _port);
        _encoding = Encoding.UTF8;
        _totalMessages = 0;
        _timeRequests = 0;
        _startTime = DateTime.Now;
        _isRunning = false;
        _logs = new Queue<ServerLog>();
    }

    private void AddLog(string message, string logLevel, string clientId = null, string clientName = null)
    {
        lock (_logLock)
        {
            var log = new ServerLog
            {
                Timestamp = DateTime.Now,
                Message = message,
                LogLevel = logLevel,
                ClientId = clientId,
                ClientName = clientName
            };

            _logs.Enqueue(log);
            while (_logs.Count > MaxLogs)
            {
                _logs.Dequeue();
            }
        }
    }

    public void Start()
    {
        lock (_stateLock)
        {
            if (_isRunning)
            {
                AddLog("Server is already running", "Warning");
                return;
            }

            try
            {
                _listener.Start();
                _isRunning = true;
                _startTime = DateTime.Now;
                _totalMessages = 0;
                _timeRequests = 0;
                AddLog($"TCP Server started on port {_port}", "Information");
                AddLog($"Server is listening on: {GetLocalIPAddress()}:{_port}", "Information");
            }
            catch (Exception ex)
            {
                AddLog($"Failed to start TCP server: {ex.Message}", "Error");
                _isRunning = false;
                throw;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Start();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                    _ = HandleClientAsync(client, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        AddLog($"Error accepting client: {ex.Message}", "Error");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AddLog($"Error in TCP server execution: {ex.Message}", "Error");
            throw;
        }
        finally
        {
            Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid().ToString();
        var clientName = $"Client_{clientId.Substring(0, 8)}";
        var clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
        
        lock (_clientLock)
        {
            _clientConnections[clientId] = client;
            _clients[clientId] = new ClientInfo 
            { 
                ClientName = clientName,
                EndPoint = clientEndPoint,
                ConnectedTime = DateTime.Now,
                LastActivityTime = DateTime.Now,
                Messages = new List<string>()
            };
        }

        AddLog($"Client connected: {clientEndPoint}", "Information", clientId, clientName);

        try
        {
            using var stream = client.GetStream();
            var buffer = new byte[1024];

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                try
                {
                    if (!client.Connected)
                    {
                        AddLog("Client disconnected", "Warning", clientId, clientName);
                        break;
                    }

                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        AddLog("Client closed connection", "Information", clientId, clientName);
                        break;
                    }

                    var message = _encoding.GetString(buffer, 0, bytesRead);
                    
                    lock (_clientLock)
                    {
                        if (_clients.ContainsKey(clientId))
                        {
                            _clients[clientId].LastActivityTime = DateTime.Now;
                            _clients[clientId].Messages.Add(message);
                        }
                    }
                    
                    lock (_stateLock)
                    {
                        _totalMessages++;
                    }

                    string response;
                    if (message.ToUpper().StartsWith("TIME:"))
                    {
                        var timeFormat = message.Substring(5).ToUpper();
                        switch (timeFormat)
                        {
                            case "DATE":
                                response = DateTime.Now.ToString("yyyy-MM-dd");
                                break;
                            case "TIME":
                                response = DateTime.Now.ToString("HH:mm:ss");
                                break;
                            default:
                                response = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                break;
                        }
                        lock (_stateLock)
                        {
                            _timeRequests++;
                        }
                        AddLog($"Time request: {timeFormat}", "Information", clientId, clientName);
                    }
                    else
                    {
                        response = $"Server received: {message}";
                        AddLog($"Message received: {message}", "Information", clientId, clientName);
                    }

                    var responseData = _encoding.GetBytes(response);
                    await stream.WriteAsync(responseData, 0, responseData.Length, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AddLog($"Error processing message: {ex.Message}", "Error", clientId, clientName);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            AddLog("Operation cancelled", "Information", clientId, clientName);
        }
        catch (Exception ex)
        {
            AddLog($"Error handling client: {ex.Message}", "Error", clientId, clientName);
        }
        finally
        {
            lock (_clientLock)
            {
                if (_clientConnections.ContainsKey(clientId))
                {
                    _clientConnections.Remove(clientId);
                    _clients.Remove(clientId);
                    try
                    {
                        client.Close();
                    }
                    catch (Exception ex)
                    {
                        AddLog($"Error closing connection: {ex.Message}", "Error", clientId, clientName);
                    }
                    AddLog("Client disconnected", "Information", clientId, clientName);
                }
            }
        }
    }

    public void Stop()
    {
        lock (_stateLock)
        {
            if (!_isRunning) return;
            _isRunning = false;
        }

        lock (_clientLock)
        {
            foreach (var client in _clientConnections.Values.ToList())
            {
                try
                {
                    client.Close();
                }
                catch (Exception ex)
                {
                    AddLog($"Error closing client connection: {ex.Message}", "Error");
                }
            }
            _clientConnections.Clear();
            _clients.Clear();
        }
        _listener?.Stop();
        AddLog("TCP Server stopped", "Information");
    }

    public void DisconnectClient(string clientId)
    {
        lock (_clientLock)
        {
            if (_clientConnections.TryGetValue(clientId, out var client))
            {
                try
                {
                    client.Close();
                    AddLog("Client disconnected", "Information", clientId);
                }
                catch (Exception ex)
                {
                    AddLog($"Error disconnecting client: {ex.Message}", "Error", clientId);
                }
                finally
                {
                    _clientConnections.Remove(clientId);
                    _clients.Remove(clientId);
                }
            }
        }
    }

    public void ResetCounters()
    {
        lock (_stateLock)
        {
            _totalMessages = 0;
            _timeRequests = 0;
            _startTime = DateTime.Now;
            AddLog("Server counters reset", "Information");
        }
    }

    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            AddLog($"Error getting local IP address: {ex.Message}", "Error");
        }
        return "127.0.0.1";
    }

    public override void Dispose()
    {
        Stop();
        base.Dispose();
    }
} 
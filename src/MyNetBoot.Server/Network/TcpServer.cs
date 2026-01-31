using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using MyNetBoot.Shared.Models;
using MyNetBoot.Shared.Network;

namespace MyNetBoot.Server.Network;

/// <summary>
/// TCP Server - Clientlar bilan aloqa
/// </summary>
public class TcpServer
{
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();
    private readonly ServerSettings _settings;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public event EventHandler<ClientConnection>? ClientConnected;
    public event EventHandler<string>? ClientDisconnected;
    public event EventHandler<(string ClientId, NetworkMessage Message)>? MessageReceived;

    public IReadOnlyDictionary<string, ClientConnection> Clients => _clients;
    public bool IsRunning => _isRunning;

    public TcpServer(ServerSettings settings)
    {
        _settings = settings;
    }

    public async Task StartAsync()
    {
        if (_isRunning) return;

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _settings.ClientPort);
        _listener.Start();
        _isRunning = true;

        Console.WriteLine($"[SERVER] Client portida tinglash boshlandi: {_settings.ClientPort}");

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _ = HandleClientAsync(tcpClient);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Client qabul qilishda xato: {ex.Message}");
                }
            }
        });
    }

    private async Task HandleClientAsync(TcpClient tcpClient)
    {
        var endpoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
        var clientId = Guid.NewGuid().ToString();
        var connection = new ClientConnection(clientId, tcpClient);

        Console.WriteLine($"[CLIENT] Yangi ulanish: {endpoint?.Address}");

        try
        {
            // Ulanish so'rovini kutamiz
            var message = await connection.ReceiveAsync();
            if (message?.Type == MessageType.Connect)
            {
                var request = message.GetPayloadAs<ConnectRequest>();
                if (request != null)
                {
                    connection.ClientInfo = new ClientInfo
                    {
                        Id = clientId,
                        Name = request.ClientName,
                        MacAddress = request.MacAddress,
                        IpAddress = endpoint?.Address.ToString() ?? "",
                        Status = ClientStatus.Online,
                        LastSeen = DateTime.Now
                    };

                    _clients.TryAdd(clientId, connection);
                    ClientConnected?.Invoke(this, connection);

                    // Tasdiqlash javobini yuboramiz
                    var response = new NetworkMessage
                    {
                        Type = MessageType.ConnectResponse,
                        SenderId = "server"
                    };
                    response.SetPayload(new ConnectResponse
                    {
                        Success = true,
                        ClientId = clientId,
                        Message = "Muvaffaqiyatli ulandi",
                        ServerVersion = "1.0.0"
                    });
                    await connection.SendAsync(response);

                    Console.WriteLine($"[CLIENT] Tasdiqlandi: {request.ClientName} ({clientId})");

                    // Xabarlarni qabul qilishni boshlaymiz
                    await ReceiveMessagesAsync(connection);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Client bilan aloqada xato: {ex.Message}");
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            ClientDisconnected?.Invoke(this, clientId);
            connection.Dispose();
            Console.WriteLine($"[CLIENT] Uzildi: {clientId}");
        }
    }

    private async Task ReceiveMessagesAsync(ClientConnection connection)
    {
        while (connection.IsConnected && !_cts!.Token.IsCancellationRequested)
        {
            var message = await connection.ReceiveAsync();
            if (message == null)
            {
                break;
            }

            connection.ClientInfo.LastSeen = DateTime.Now;

            if (message.Type == MessageType.Heartbeat)
            {
                var response = new NetworkMessage
                {
                    Type = MessageType.HeartbeatResponse,
                    SenderId = "server"
                };
                await connection.SendAsync(response);
            }
            else if (message.Type == MessageType.Disconnect)
            {
                break;
            }
            else
            {
                MessageReceived?.Invoke(this, (connection.ClientInfo.Id, message));
            }
        }
    }

    public async Task SendToClientAsync(string clientId, NetworkMessage message)
    {
        if (_clients.TryGetValue(clientId, out var connection))
        {
            await connection.SendAsync(message);
        }
    }

    public async Task BroadcastAsync(NetworkMessage message)
    {
        foreach (var connection in _clients.Values)
        {
            try
            {
                await connection.SendAsync(message);
            }
            catch { }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _isRunning = false;

        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();

        Console.WriteLine("[SERVER] To'xtatildi");
    }
}

/// <summary>
/// Client ulanishi
/// </summary>
public class ClientConnection : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string Id { get; }
    public ClientInfo ClientInfo { get; set; } = new();
    public bool IsConnected => _client.Connected;
    
    // User session tracking
    public int UserId { get; set; }
    public DateTime SessionStartTime { get; set; }
    public decimal InitialBalans { get; set; }

    public ClientConnection(string id, TcpClient client)
    {
        Id = id;
        _client = client;
        _stream = client.GetStream();
    }

    public async Task SendAsync(NetworkMessage message)
    {
        await _sendLock.WaitAsync();
        try
        {
            var data = message.ToBytes();
            await _stream.WriteAsync(data);
            await _stream.FlushAsync();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<NetworkMessage?> ReceiveAsync()
    {
        try
        {
            var lengthBuffer = new byte[4];
            var read = await _stream.ReadAsync(lengthBuffer);
            if (read == 0) return null;

            var length = BitConverter.ToInt32(lengthBuffer);
            if (length <= 0 || length > 10_000_000) return null;

            var dataBuffer = new byte[length];
            var totalRead = 0;
            while (totalRead < length)
            {
                read = await _stream.ReadAsync(dataBuffer.AsMemory(totalRead));
                if (read == 0) return null;
                totalRead += read;
            }

            return NetworkMessage.FromBytes(dataBuffer);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _sendLock.Dispose();
    }
}

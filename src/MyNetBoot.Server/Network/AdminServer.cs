using System.Net;
using System.Net.Sockets;
using MyNetBoot.Shared.Models;
using MyNetBoot.Shared.Network;

namespace MyNetBoot.Server.Network;

/// <summary>
/// Admin TCP Server - Admin panel bilan aloqa
/// </summary>
public class AdminServer
{
    private TcpListener? _listener;
    private TcpClient? _adminClient;
    private NetworkStream? _adminStream;
    private readonly ServerSettings _settings;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public event EventHandler<NetworkMessage>? MessageReceived;
    public event EventHandler? AdminConnected;
    public event EventHandler? AdminDisconnected;

    public bool IsRunning => _isRunning;
    public bool IsAdminConnected => _adminClient?.Connected ?? false;

    public AdminServer(ServerSettings settings)
    {
        _settings = settings;
    }

    public async Task StartAsync()
    {
        if (_isRunning) return;

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _settings.AdminPort);
        _listener.Start();
        _isRunning = true;

        Console.WriteLine($"[ADMIN] Admin portida tinglash boshlandi: {_settings.AdminPort}");

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    await HandleAdminAsync(client);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ADMIN ERROR] {ex.Message}");
                }
            }
        });
    }

    private async Task HandleAdminAsync(TcpClient client)
    {
        // Faqat bitta admin ulanishiga ruxsat
        if (_adminClient != null)
        {
            client.Close();
            return;
        }

        _adminClient = client;
        _adminStream = client.GetStream();
        var endpoint = client.Client.RemoteEndPoint as IPEndPoint;

        Console.WriteLine($"[ADMIN] Ulandi: {endpoint?.Address}");

        try
        {
            // Autentifikatsiya so'rovini kutamiz
            var authMessage = await ReceiveAsync();
            if (authMessage?.Type == MessageType.AdminAuth)
            {
                // TODO: Parolni tekshirish
                var response = new NetworkMessage
                {
                    Type = MessageType.AdminAuthResponse,
                    SenderId = "server"
                };
                response.SetPayload(new { Success = true, Message = "Muvaffaqiyatli" });
                await SendAsync(response);

                AdminConnected?.Invoke(this, EventArgs.Empty);

                // Xabarlarni qabul qilish
                while (_adminClient.Connected && !_cts!.Token.IsCancellationRequested)
                {
                    var message = await ReceiveAsync();
                    if (message == null) break;

                    MessageReceived?.Invoke(this, message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ADMIN ERROR] {ex.Message}");
        }
        finally
        {
            _adminStream?.Dispose();
            _adminClient?.Dispose();
            _adminClient = null;
            _adminStream = null;
            AdminDisconnected?.Invoke(this, EventArgs.Empty);
            Console.WriteLine("[ADMIN] Uzildi");
        }
    }

    public async Task SendAsync(NetworkMessage message)
    {
        if (_adminStream == null) return;

        await _sendLock.WaitAsync();
        try
        {
            var data = message.ToBytes();
            await _adminStream.WriteAsync(data);
            await _adminStream.FlushAsync();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<NetworkMessage?> ReceiveAsync()
    {
        if (_adminStream == null) return null;

        try
        {
            var lengthBuffer = new byte[4];
            var read = await _adminStream.ReadAsync(lengthBuffer);
            if (read == 0) return null;

            var length = BitConverter.ToInt32(lengthBuffer);
            if (length <= 0 || length > 10_000_000) return null;

            var dataBuffer = new byte[length];
            var totalRead = 0;
            while (totalRead < length)
            {
                read = await _adminStream.ReadAsync(dataBuffer.AsMemory(totalRead));
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

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _adminStream?.Dispose();
        _adminClient?.Dispose();
        _isRunning = false;
        Console.WriteLine("[ADMIN] To'xtatildi");
    }
}

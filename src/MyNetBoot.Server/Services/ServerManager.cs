using System.Diagnostics;
using System.Text.Json;
using MyNetBoot.Shared.Models;
using MyNetBoot.Shared.Network;
using MyNetBoot.Server.Network;

namespace MyNetBoot.Server.Services;

/// <summary>
/// Asosiy server boshqaruvchisi
/// </summary>
public class ServerManager
{
    private readonly ServerSettings _settings;
    private readonly TcpServer _tcpServer;
    private readonly AdminServer _adminServer;
    private readonly GameService _gameService;
    private readonly FileTransferService _fileTransferService;
    private readonly UserService _userService;
    private readonly DateTime _startTime;
    private long _totalBytesTransferred;

    public ServerSettings Settings => _settings;
    public TcpServer TcpServer => _tcpServer;
    public AdminServer AdminServer => _adminServer;
    public GameService GameService => _gameService;
    public UserService UserService => _userService;

    public ServerManager()
    {
        _settings = LoadSettings();
        _tcpServer = new TcpServer(_settings);
        _adminServer = new AdminServer(_settings);
        _gameService = new GameService(_settings.DataPath);
        _fileTransferService = new FileTransferService(_gameService, _settings.FileTransferBufferSize);
        _userService = new UserService(_settings.DataPath);
        _startTime = DateTime.Now;

        // Event handlerlar
        _tcpServer.ClientConnected += OnClientConnected;
        _tcpServer.ClientDisconnected += OnClientDisconnected;
        _tcpServer.MessageReceived += OnClientMessage;
        _adminServer.MessageReceived += OnAdminMessage;
    }

    private ServerSettings LoadSettings()
    {
        var settingsPath = Path.Combine("data", "config", "server.json");
        if (File.Exists(settingsPath))
        {
            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<ServerSettings>(json) ?? new ServerSettings();
        }
        return new ServerSettings();
    }

    public async Task SaveSettingsAsync()
    {
        var settingsPath = Path.Combine(_settings.DataPath, "config", "server.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(settingsPath, json);
    }

    public async Task StartAsync()
    {
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║      MyNetBoot Server v1.0.0           ║");
        Console.WriteLine("║      CCBoot Alternative                ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
        Console.WriteLine();

        await _gameService.LoadAsync();
        await _tcpServer.StartAsync();
        await _adminServer.StartAsync();

        Console.WriteLine();
        Console.WriteLine("[SERVER] Tayyor. Ctrl+C - to'xtatish.");
    }

    public void Stop()
    {
        _tcpServer.Stop();
        _adminServer.Stop();
    }

    private void OnClientConnected(object? sender, ClientConnection connection)
    {
        // Admin panelga xabar yuborish
        SendClientListToAdmin().ConfigureAwait(false);
    }

    private void OnClientDisconnected(object? sender, string clientId)
    {
        SendClientListToAdmin().ConfigureAwait(false);
    }

    private async void OnClientMessage(object? sender, (string ClientId, NetworkMessage Message) args)
    {
        var (clientId, message) = args;

        switch (message.Type)
        {
            case MessageType.GetGameList:
                await HandleGetGameListAsync(clientId);
                break;

            case MessageType.GetGameInfo:
                var gameId = message.Payload;
                await HandleGetGameInfoAsync(clientId, gameId);
                break;

            case MessageType.RequestFile:
                var request = message.GetPayloadAs<FileRequest>();
                if (request != null)
                {
                    await HandleFileRequestAsync(clientId, request);
                }
                break;

            case MessageType.LaunchGame:
                await HandleGameLaunchedAsync(clientId, message.Payload);
                break;

            case MessageType.GameClosed:
                await HandleGameClosedAsync(clientId);
                break;
                
            case MessageType.UserLogin:
                var loginRequest = message.GetPayloadAs<UserLoginRequest>();
                if (loginRequest != null)
                {
                    await HandleUserLoginAsync(clientId, loginRequest);
                }
                break;
                
            case MessageType.UserLogout:
                await HandleUserLogoutAsync(clientId);
                break;
                
            case MessageType.UserBalansUpdate:
                var balansUpdate = message.GetPayloadAs<BalansUpdate>();
                if (balansUpdate != null)
                {
                    await HandleBalansUpdateAsync(clientId, balansUpdate);
                }
                break;
        }
    }

    private async Task HandleGetGameListAsync(string clientId)
    {
        var response = new NetworkMessage
        {
            Type = MessageType.GameListResponse,
            SenderId = "server"
        };
        response.SetPayload(_gameService.Games.Where(g => g.IsEnabled).ToList());
        await _tcpServer.SendToClientAsync(clientId, response);
    }

    private async Task HandleGetGameInfoAsync(string clientId, string gameId)
    {
        var game = _gameService.GetGame(gameId);
        var response = new NetworkMessage
        {
            Type = MessageType.GameInfoResponse,
            SenderId = "server"
        };
        response.SetPayload(game);
        await _tcpServer.SendToClientAsync(clientId, response);
    }

    private async Task HandleFileRequestAsync(string clientId, FileRequest request)
    {
        var chunk = await _fileTransferService.GetFileChunkAsync(request);
        if (chunk != null)
        {
            var response = new NetworkMessage
            {
                Type = MessageType.FileChunk,
                SenderId = "server"
            };
            response.SetPayload(chunk);
            await _tcpServer.SendToClientAsync(clientId, response);
            _totalBytesTransferred += chunk.Data.Length;
        }
        else
        {
            var response = new NetworkMessage
            {
                Type = MessageType.FileTransferError,
                SenderId = "server",
                Payload = "Fayl topilmadi"
            };
            await _tcpServer.SendToClientAsync(clientId, response);
        }
    }

    private async Task HandleGameLaunchedAsync(string clientId, string gameId)
    {
        if (_tcpServer.Clients.TryGetValue(clientId, out var connection))
        {
            var game = _gameService.GetGame(gameId);
            connection.ClientInfo.Status = ClientStatus.Playing;
            connection.ClientInfo.CurrentGame = game?.Name ?? "";
        }
        await SendClientListToAdmin();
    }

    private async Task HandleGameClosedAsync(string clientId)
    {
        if (_tcpServer.Clients.TryGetValue(clientId, out var connection))
        {
            connection.ClientInfo.Status = ClientStatus.Online;
            connection.ClientInfo.CurrentGame = "";
        }
        await SendClientListToAdmin();
    }

    private async void OnAdminMessage(object? sender, NetworkMessage message)
    {
        switch (message.Type)
        {
            case MessageType.GetClients:
                await SendClientListToAdmin();
                break;

            case MessageType.GetStats:
                await SendStatsToAdmin();
                break;

            case MessageType.GetGameList:
                await SendGameListToAdmin();
                break;
            
            case MessageType.GetUsers:
                await SendUsersListToAdmin();
                break;

            case MessageType.AddGame:
                var newGame = message.GetPayloadAs<GameInfo>();
                if (newGame != null)
                {
                    await _gameService.AddGameAsync(newGame);
                    await SendGameListToAdmin();
                }
                break;

            case MessageType.RemoveGame:
                await _gameService.RemoveGameAsync(message.Payload);
                await SendGameListToAdmin();
                break;

            case MessageType.BlockClient:
                await BlockClientAsync(message.Payload);
                break;

            case MessageType.KickClient:
                await KickClientAsync(message.Payload);
                break;

            case MessageType.SendMessage:
                // TODO: Clientga xabar yuborish
                break;
        }
    }

    private async Task SendClientListToAdmin()
    {
        var clients = _tcpServer.Clients.Values.Select(c => c.ClientInfo).ToList();
        var response = new NetworkMessage
        {
            Type = MessageType.ClientsResponse,
            SenderId = "server"
        };
        response.SetPayload(clients);
        await _adminServer.SendAsync(response);
    }

    private async Task SendStatsToAdmin()
    {
        var stats = new ServerStats
        {
            TotalClients = _tcpServer.Clients.Count,
            OnlineClients = _tcpServer.Clients.Values.Count(c => c.ClientInfo.Status == ClientStatus.Online),
            PlayingClients = _tcpServer.Clients.Values.Count(c => c.ClientInfo.Status == ClientStatus.Playing),
            TotalGames = _gameService.Games.Count,
            TotalBytesTransferred = _totalBytesTransferred,
            Uptime = DateTime.Now - _startTime,
            CpuUsage = GetCpuUsage(),
            MemoryUsage = Process.GetCurrentProcess().WorkingSet64
        };

        var response = new NetworkMessage
        {
            Type = MessageType.StatsResponse,
            SenderId = "server"
        };
        response.SetPayload(stats);
        await _adminServer.SendAsync(response);
    }

    private async Task SendGameListToAdmin()
    {
        var response = new NetworkMessage
        {
            Type = MessageType.GameListResponse,
            SenderId = "server"
        };
        response.SetPayload(_gameService.Games.ToList());
        await _adminServer.SendAsync(response);
    }

    private async Task SendUsersListToAdmin()
    {
        var users = _userService.GetAllUsers();
        var response = new NetworkMessage
        {
            Type = MessageType.UsersResponse,
            SenderId = "server"
        };
        response.SetPayload(users);
        await _adminServer.SendAsync(response);
    }

    private async Task BlockClientAsync(string clientId)
    {
        if (_tcpServer.Clients.TryGetValue(clientId, out var connection))
        {
            connection.ClientInfo.IsBlocked = true;
            
            // Avval ForceDisconnect yuboramiz
            try
            {
                var forceDisconnect = new NetworkMessage
                {
                    Type = MessageType.ForceDisconnect,
                    SenderId = "server",
                    Payload = "Siz bloklangansiz. Administrator bilan bog'laning."
                };
                await connection.SendAsync(forceDisconnect);
                await Task.Delay(100); // Xabarni olish uchun vaqt
            }
            catch { }
            
            connection.Dispose();
            Console.WriteLine($"[ADMIN] Client bloklandi: {connection.ClientInfo.Name}");
        }
        await SendClientListToAdmin();
    }

    private async Task KickClientAsync(string clientId)
    {
        if (_tcpServer.Clients.TryGetValue(clientId, out var connection))
        {
            // Avval ForceDisconnect yuboramiz - bu o'yinni yopadi
            try
            {
                var forceDisconnect = new NetworkMessage
                {
                    Type = MessageType.ForceDisconnect,
                    SenderId = "server",
                    Payload = "Administrator tomonidan uzilingan."
                };
                await connection.SendAsync(forceDisconnect);
                await Task.Delay(100); // Xabarni olish uchun vaqt
            }
            catch { }
            
            connection.Dispose();
            Console.WriteLine($"[ADMIN] Client uzildi: {connection.ClientInfo.Name}");
        }
        await SendClientListToAdmin();
    }

    private double GetCpuUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount / 1000.0;
        }
        catch
        {
            return 0;
        }
    }

    #region User Login/Logout Handlers
    
    private async Task HandleUserLoginAsync(string clientId, UserLoginRequest loginRequest)
    {
        var response = new NetworkMessage
        {
            Type = MessageType.UserLoginResponse,
            SenderId = "server"
        };

        var user = _userService.Login(loginRequest.TelefonRaqam, loginRequest.Parol);
        
        if (user == null)
        {
            response.SetPayload(new UserLoginResponse
            {
                Success = false,
                Message = "Telefon raqam yoki parol noto'g'ri!"
            });
        }
        else if (user.Holat == UserHolat.Block)
        {
            response.SetPayload(new UserLoginResponse
            {
                Success = false,
                Message = "Sizning hisobingiz bloklangan. Administrator bilan bog'laning."
            });
        }
        else if (user.Balans <= 0)
        {
            response.SetPayload(new UserLoginResponse
            {
                Success = false,
                Message = "Balans yetarli emas. Iltimos, balansni to'ldiring."
            });
        }
        else
        {
            response.SetPayload(new UserLoginResponse
            {
                Success = true,
                Message = $"Xush kelibsiz, {user.Ism}!",
                UserId = user.Id,
                Familya = user.Familya,
                Ism = user.Ism,
                Balans = user.Balans,
                Holat = user.Holat.ToString()
            });
            
            // Client ma'lumotlarini yangilash
            if (_tcpServer.Clients.TryGetValue(clientId, out var connection))
            {
                connection.ClientInfo.Name = $"{user.Familya} {user.Ism}";
                connection.UserId = user.Id;
                connection.SessionStartTime = DateTime.Now;
                connection.InitialBalans = user.Balans;
            }
            
            Console.WriteLine($"[USER] Kirdi: {user.Familya} {user.Ism} (Balans: {user.Balans})");
        }

        await _tcpServer.SendToClientAsync(clientId, response);
    }

    private async Task HandleUserLogoutAsync(string clientId)
    {
        if (_tcpServer.Clients.TryGetValue(clientId, out var connection) && connection.UserId > 0)
        {
            var user = _userService.GetById(connection.UserId);
            if (user != null)
            {
                // Seans tarixini saqlash
                var tugashVaqti = DateTime.Now;
                var oynalganMinut = (int)(tugashVaqti - connection.SessionStartTime).TotalMinutes;
                var yechilganBalans = connection.InitialBalans - user.Balans;
                
                if (oynalganMinut > 0 || yechilganBalans > 0)
                {
                    var seans = new UserSeans
                    {
                        UserId = user.Id,
                        Familya = user.Familya,
                        Ism = user.Ism,
                        TelefonRaqam = user.TelefonRaqam,
                        YechilganBalans = yechilganBalans,
                        BoshlashVaqti = connection.SessionStartTime,
                        TugashVaqti = tugashVaqti,
                        OynalganMinut = oynalganMinut
                    };
                    _userService.SaveSeans(seans);
                }
                
                // Holatni faol qilish (o'yinni tugatgani uchun)
                _userService.UpdateHolat(user.Id, UserHolat.Faol);
                Console.WriteLine($"[USER] Chiqdi: {user.Familya} {user.Ism} (Qolgan balans: {user.Balans}, O'ynagan: {oynalganMinut} min)");
            }
            connection.UserId = 0;
        }
    }

    private async Task HandleBalansUpdateAsync(string clientId, BalansUpdate update)
    {
        // Balansni yangilash
        _userService.UpdateBalans(update.UserId, update.YangiBalans);
        
        // Agar balans 0 ga tushgan bo'lsa - foydalanuvchini bloklash va uzish
        if (update.YangiBalans <= 0)
        {
            _userService.UpdateHolat(update.UserId, UserHolat.Block);
            
            var forceDisconnect = new NetworkMessage
            {
                Type = MessageType.ForceDisconnect,
                SenderId = "server",
                Payload = "Balans tugadi. Hisobingizni to'ldiring."
            };
            await _tcpServer.SendToClientAsync(clientId, forceDisconnect);
            
            var user = _userService.GetById(update.UserId);
            Console.WriteLine($"[USER] Balans tugadi: {user?.Familya} {user?.Ism}");
        }
    }
    
    #endregion
}


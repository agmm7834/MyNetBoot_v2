using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;
using MyNetBoot.Shared.Models;
using MyNetBoot.Shared.Network;

namespace MyNetBoot.Admin.ViewModels;

/// <summary>
/// Asosiy oyna ViewModel
/// </summary>
public class MainViewModel : ViewModelBase
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _autoRefreshTimer;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    
    private string _serverAddress = "127.0.0.1";
    private int _serverPort = 8801;
    private bool _isConnected;
    private string _connectionStatus = "Ulanmagan";
    private string _currentView = "Dashboard";
    private bool _isAutoRefreshEnabled = true;
    private int _autoRefreshInterval = 2; // soniyada
    
    // Statistika
    private int _totalClients;
    private int _onlineClients;
    private int _playingClients;
    private int _totalGames;
    private string _uptime = "00:00:00";
    private string _bandwidth = "0 KB/s";
    private string _lastUpdated = "";
    
    public ObservableCollection<ClientInfo> Clients { get; } = new();
    public ObservableCollection<GameInfo> Games { get; } = new();
    public ObservableCollection<User> Users { get; } = new();

    #region Properties
    public string ServerAddress
    {
        get => _serverAddress;
        set => SetProperty(ref _serverAddress, value);
    }
    
    public int ServerPort
    {
        get => _serverPort;
        set => SetProperty(ref _serverPort, value);
    }
    
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            SetProperty(ref _isConnected, value);
            OnPropertyChanged(nameof(IsNotConnected));
        }
    }
    
    public bool IsNotConnected => !IsConnected;
    
    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }
    
    public string CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }
    
    public bool IsAutoRefreshEnabled
    {
        get => _isAutoRefreshEnabled;
        set
        {
            SetProperty(ref _isAutoRefreshEnabled, value);
            UpdateAutoRefreshTimer();
        }
    }
    
    public int AutoRefreshInterval
    {
        get => _autoRefreshInterval;
        set
        {
            SetProperty(ref _autoRefreshInterval, Math.Max(1, Math.Min(30, value)));
            UpdateAutoRefreshTimer();
        }
    }
    
    public int TotalClients
    {
        get => _totalClients;
        set => SetProperty(ref _totalClients, value);
    }
    
    public int OnlineClients
    {
        get => _onlineClients;
        set => SetProperty(ref _onlineClients, value);
    }
    
    public int PlayingClients
    {
        get => _playingClients;
        set => SetProperty(ref _playingClients, value);
    }
    
    public int TotalGames
    {
        get => _totalGames;
        set => SetProperty(ref _totalGames, value);
    }
    
    public string Uptime
    {
        get => _uptime;
        set => SetProperty(ref _uptime, value);
    }
    
    public string Bandwidth
    {
        get => _bandwidth;
        set => SetProperty(ref _bandwidth, value);
    }
    
    public string LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }
    #endregion

    #region Commands
    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand AddGameCommand { get; }
    public RelayCommand RemoveGameCommand { get; }
    public RelayCommand KickClientCommand { get; }
    public RelayCommand BlockClientCommand { get; }
    public RelayCommand ToggleAutoRefreshCommand { get; }
    #endregion

    public MainViewModel()
    {
        ConnectCommand = new RelayCommand(_ => ConnectAsync(), _ => !IsConnected);
        DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsConnected);
        RefreshCommand = new RelayCommand(_ => RefreshDataAsync());
        NavigateCommand = new RelayCommand(param => CurrentView = param?.ToString() ?? "Dashboard");
        AddGameCommand = new RelayCommand(_ => AddGame());
        RemoveGameCommand = new RelayCommand(param => RemoveGame(param?.ToString()));
        KickClientCommand = new RelayCommand(param => KickClient(param?.ToString()));
        BlockClientCommand = new RelayCommand(param => BlockClient(param?.ToString()));
        ToggleAutoRefreshCommand = new RelayCommand(_ => IsAutoRefreshEnabled = !IsAutoRefreshEnabled);
        
        // Auto-refresh timer yaratish
        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_autoRefreshInterval)
        };
        _autoRefreshTimer.Tick += async (s, e) =>
        {
            if (IsConnected && IsAutoRefreshEnabled)
            {
                await RefreshDataAsync();
            }
        };
    }

    private void UpdateAutoRefreshTimer()
    {
        if (_autoRefreshTimer == null) return;
        
        _autoRefreshTimer.Stop();
        if (IsAutoRefreshEnabled && IsConnected)
        {
            _autoRefreshTimer.Interval = TimeSpan.FromSeconds(_autoRefreshInterval);
            _autoRefreshTimer.Start();
        }
    }

    private async void ConnectAsync()
    {
        try
        {
            ConnectionStatus = "Ulanmoqda...";
            _client = new TcpClient();
            await _client.ConnectAsync(ServerAddress, ServerPort);
            _stream = _client.GetStream();
            
            // Autentifikatsiya
            var authMessage = new NetworkMessage
            {
                Type = MessageType.AdminAuth,
                SenderId = "admin"
            };
            authMessage.SetPayload(new { Password = "admin" });
            await SendMessageAsync(authMessage);
            
            var response = await ReceiveMessageAsync();
            if (response?.Type == MessageType.AdminAuthResponse)
            {
                IsConnected = true;
                ConnectionStatus = "Ulandi ✓";
                
                _cts = new CancellationTokenSource();
                _ = ReceiveMessagesAsync();
                
                // Ma'lumotlarni yuklash
                await RefreshDataAsync();
                
                // Auto-refresh boshlash
                UpdateAutoRefreshTimer();
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Xato: {ex.Message}";
            Disconnect();
        }
    }

    private void Disconnect()
    {
        _autoRefreshTimer?.Stop();
        _cts?.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
        _client = null;
        _stream = null;
        IsConnected = false;
        ConnectionStatus = "Ulanmagan";
        Clients.Clear();
        Games.Clear();
        LastUpdated = "";
    }

    private async Task RefreshDataAsync()
    {
        if (!IsConnected) return;
        
        try
        {
            await SendMessageAsync(new NetworkMessage { Type = MessageType.GetClients, SenderId = "admin" });
            await SendMessageAsync(new NetworkMessage { Type = MessageType.GetStats, SenderId = "admin" });
            await SendMessageAsync(new NetworkMessage { Type = MessageType.GetGameList, SenderId = "admin" });
            await SendMessageAsync(new NetworkMessage { Type = MessageType.GetUsers, SenderId = "admin" });
            
            LastUpdated = $"Yangilandi: {DateTime.Now:HH:mm:ss}";
        }
        catch { }
    }

    private async Task ReceiveMessagesAsync()
    {
        while (!_cts!.Token.IsCancellationRequested && IsConnected)
        {
            try
            {
                var message = await ReceiveMessageAsync();
                if (message == null)
                {
                    Application.Current.Dispatcher.Invoke(Disconnect);
                    break;
                }
                
                await Application.Current.Dispatcher.InvokeAsync(() => HandleMessage(message));
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(Disconnect);
                break;
            }
        }
    }

    private void HandleMessage(NetworkMessage message)
    {
        switch (message.Type)
        {
            case MessageType.ClientsResponse:
                var clients = message.GetPayloadAs<List<ClientInfo>>();
                if (clients != null)
                {
                    // Ma'lumotlarni yangilash (eski usul o'rniga smart update)
                    UpdateClientsCollection(clients);
                    TotalClients = clients.Count;
                    OnlineClients = clients.Count(c => c.Status == ClientStatus.Online || c.Status == ClientStatus.Playing);
                    PlayingClients = clients.Count(c => c.Status == ClientStatus.Playing);
                }
                break;

            case MessageType.StatsResponse:
                var stats = message.GetPayloadAs<ServerStats>();
                if (stats != null)
                {
                    OnlineClients = stats.OnlineClients;
                    PlayingClients = stats.PlayingClients;
                    TotalGames = stats.TotalGames;
                    Uptime = stats.Uptime.ToString(@"hh\:mm\:ss");
                    Bandwidth = FormatBytes(stats.TotalBytesTransferred);
                }
                break;

            case MessageType.GameListResponse:
                var games = message.GetPayloadAs<List<GameInfo>>();
                if (games != null)
                {
                    Games.Clear();
                    foreach (var game in games)
                    {
                        Games.Add(game);
                    }
                    TotalGames = games.Count;
                }
                break;
                
            case MessageType.UsersResponse:
                var users = message.GetPayloadAs<List<User>>();
                if (users != null)
                {
                    Users.Clear();
                    foreach (var user in users)
                    {
                        Users.Add(user);
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Clientlar kolleksiyasini aqlli yangilash - scroll pozitsiyasini saqlab
    /// </summary>
    private void UpdateClientsCollection(List<ClientInfo> newClients)
    {
        var existingIds = Clients.Select(c => c.Id).ToHashSet();
        var newIds = newClients.Select(c => c.Id).ToHashSet();
        
        // O'chirilgan clientlarni olib tashlash
        var toRemove = Clients.Where(c => !newIds.Contains(c.Id)).ToList();
        foreach (var client in toRemove)
        {
            Clients.Remove(client);
        }
        
        // Yangi yoki yangilangan clientlarni qo'shish/yangilash
        foreach (var newClient in newClients)
        {
            var existing = Clients.FirstOrDefault(c => c.Id == newClient.Id);
            if (existing != null)
            {
                // Mavjud clientni yangilash
                existing.Name = newClient.Name;
                existing.Status = newClient.Status;
                existing.IpAddress = newClient.IpAddress;
                existing.CurrentGame = newClient.CurrentGame;
                existing.LastSeen = newClient.LastSeen;
                existing.IsBlocked = newClient.IsBlocked;
            }
            else
            {
                // Yangi client qo'shish
                Clients.Add(newClient);
            }
        }
    }

    private async Task SendMessageAsync(NetworkMessage message)
    {
        if (_stream == null) return;
        
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

    private async Task<NetworkMessage?> ReceiveMessageAsync()
    {
        if (_stream == null) return null;
        
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

    private async void AddGame()
    {
        var game = new GameInfo
        {
            Name = "Yangi O'yin",
            Category = "Action",
            IsEnabled = true
        };
        
        var message = new NetworkMessage
        {
            Type = MessageType.AddGame,
            SenderId = "admin"
        };
        message.SetPayload(game);
        await SendMessageAsync(message);
    }

    private async void RemoveGame(string? gameId)
    {
        if (string.IsNullOrEmpty(gameId)) return;
        
        var result = MessageBox.Show("O'yinni o'chirishni xohlaysizmi?", "Tasdiqlash", 
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        
        var message = new NetworkMessage
        {
            Type = MessageType.RemoveGame,
            SenderId = "admin",
            Payload = gameId
        };
        await SendMessageAsync(message);
    }

    private async void KickClient(string? clientId)
    {
        if (string.IsNullOrEmpty(clientId)) return;
        
        var message = new NetworkMessage
        {
            Type = MessageType.KickClient,
            SenderId = "admin",
            Payload = clientId
        };
        await SendMessageAsync(message);
    }

    private async void BlockClient(string? clientId)
    {
        if (string.IsNullOrEmpty(clientId)) return;
        
        var message = new NetworkMessage
        {
            Type = MessageType.BlockClient,
            SenderId = "admin",
            Payload = clientId
        };
        await SendMessageAsync(message);
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

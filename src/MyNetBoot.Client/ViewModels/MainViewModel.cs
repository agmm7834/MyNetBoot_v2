using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MyNetBoot.Shared.Models;
using MyNetBoot.Shared.Network;

namespace MyNetBoot.Client.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Process? _runningGameProcess;
    private string? _runningGameName;
    private DispatcherTimer? _balansTimer;
    private DateTime _sessionStartTime;
    
    private string _serverAddress = "127.0.0.1";
    private int _serverPort = 8800;
    private string _clientName = Environment.MachineName;
    private bool _isConnected;
    private string _connectionStatus = "Server bilan ulanmagan";
    private GameInfo? _selectedGame;
    private double _downloadProgress;
    private string _downloadStatus = "";
    private bool _isPlaying;
    
    // Login fields
    private string _telefonRaqam = "";
    private string _parol = "";
    private string _loginError = "";
    private bool _isLoggedIn;
    private bool _isLoggingIn;
    private int _userId;
    private string _userFullName = "";
    private decimal _currentBalans;
    private string _balansText = "";
    
    public ObservableCollection<GameInfo> Games { get; } = new();
    
    public string ServerAddress
    {
        get => _serverAddress;
        set { _serverAddress = value; OnPropertyChanged(); }
    }
    
    public int ServerPort
    {
        get => _serverPort;
        set { _serverPort = value; OnPropertyChanged(); }
    }
    
    public string ClientName
    {
        get => _clientName;
        set { _clientName = value; OnPropertyChanged(); }
    }
    
    public bool IsConnected
    {
        get => _isConnected;
        set 
        { 
            _isConnected = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(IsNotConnected)); 
            OnPropertyChanged(nameof(ShowLoginForm)); 
            OnPropertyChanged(nameof(ShowGameContent)); 
        }
    }
    
    public bool IsNotConnected => !IsConnected;
    
    public string ConnectionStatus
    {
        get => _connectionStatus;
        set { _connectionStatus = value; OnPropertyChanged(); }
    }
    
    public GameInfo? SelectedGame
    {
        get => _selectedGame;
        set { _selectedGame = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelectedGame)); }
    }
    
    public bool HasSelectedGame => SelectedGame != null;
    
    public double DownloadProgress
    {
        get => _downloadProgress;
        set { _downloadProgress = value; OnPropertyChanged(); }
    }
    
    public string DownloadStatus
    {
        get => _downloadStatus;
        set { _downloadStatus = value; OnPropertyChanged(); }
    }
    
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); }
    }
    
    // Login properties
    public string TelefonRaqam
    {
        get => _telefonRaqam;
        set { _telefonRaqam = value; OnPropertyChanged(); LoginError = ""; }
    }
    
    public string Parol
    {
        get => _parol;
        set { _parol = value; OnPropertyChanged(); LoginError = ""; }
    }
    
    public string LoginError
    {
        get => _loginError;
        set { _loginError = value; OnPropertyChanged(); }
    }
    
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set { _isLoggedIn = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowLoginForm)); OnPropertyChanged(nameof(ShowGameContent)); }
    }
    
    public bool IsLoggingIn
    {
        get => _isLoggingIn;
        set { _isLoggingIn = value; OnPropertyChanged(); }
    }
    
    public string UserFullName
    {
        get => _userFullName;
        set { _userFullName = value; OnPropertyChanged(); }
    }
    
    public decimal CurrentBalans
    {
        get => _currentBalans;
        set { _currentBalans = value; OnPropertyChanged(); BalansText = $"{value:N0} so'm"; }
    }
    
    public string BalansText
    {
        get => _balansText;
        set { _balansText = value; OnPropertyChanged(); }
    }
    
    public bool ShowLoginForm => IsConnected && !IsLoggedIn;
    public bool ShowGameContent => IsConnected && IsLoggedIn;
    
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand PlayGameCommand { get; }
    public ICommand SelectGameCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand EndSessionCommand { get; }
    
    public MainViewModel()
    {
        ConnectCommand = new RelayCommand(_ => ConnectAsync(), _ => !IsConnected);
        DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsConnected);
        PlayGameCommand = new RelayCommand(_ => PlayGame(), _ => IsConnected && IsLoggedIn && SelectedGame != null && !IsPlaying);
        SelectGameCommand = new RelayCommand(param => SelectGame(param as GameInfo));
        LoginCommand = new RelayCommand(_ => LoginAsync(), _ => IsConnected && !IsLoggedIn && !IsLoggingIn);
        EndSessionCommand = new RelayCommand(_ => EndSession(), _ => IsLoggedIn);
        
        // Balance timer setup
        _balansTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _balansTimer.Tick += BalansTimer_Tick;
    }
    
    private async void ConnectAsync()
    {
        try
        {
            ConnectionStatus = "Ulanmoqda...";
            _client = new TcpClient();
            await _client.ConnectAsync(ServerAddress, ServerPort);
            _stream = _client.GetStream();
            
            // Ulanish so'rovi
            var connectMessage = new NetworkMessage
            {
                Type = MessageType.Connect,
                SenderId = "client"
            };
            connectMessage.SetPayload(new ConnectRequest
            {
                ClientName = ClientName,
                MacAddress = GetMacAddress(),
                Version = "1.0.0"
            });
            await SendMessageAsync(connectMessage);
            
            var response = await ReceiveMessageAsync();
            if (response?.Type == MessageType.ConnectResponse)
            {
                var connectResponse = response.GetPayloadAs<ConnectResponse>();
                if (connectResponse?.Success == true)
                {
                    IsConnected = true;
                    ConnectionStatus = "Ulandi ✓";
                    
                    _cts = new CancellationTokenSource();
                    _ = ReceiveMessagesAsync();
                    _ = HeartbeatAsync();
                    
                    // O'yinlar ro'yxatini so'rash
                    await SendMessageAsync(new NetworkMessage { Type = MessageType.GetGameList, SenderId = "client" });
                }
                else
                {
                    ConnectionStatus = connectResponse?.Message ?? "Ulanish rad etildi";
                    Disconnect();
                }
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Xato: {ex.Message}";
            Disconnect();
        }
    }
    
    private void Disconnect(bool wasForced = false)
    {
        // Timer ni to'xtatish
        _balansTimer?.Stop();
        
        // O'yin o'ynayotgan bo'lsa - yopish
        CloseRunningGame(wasForced ? "Server bilan aloqa uzildi. O'yin yopildi." : null);
        
        // Login state ni tozalash
        ResetLoginState();
        
        try
        {
            if (IsConnected && !wasForced)
            {
                _ = SendMessageAsync(new NetworkMessage { Type = MessageType.Disconnect, SenderId = "client" });
            }
        }
        catch { }
        
        _cts?.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
        _client = null;
        _stream = null;
        IsConnected = false;
        ConnectionStatus = wasForced ? "Server tomonidan uzildi" : "Server bilan ulanmagan";
        Games.Clear();
        SelectedGame = null;
    }
    
    /// <summary>
    /// Ishlab turgan o'yinni yopish
    /// </summary>
    private void CloseRunningGame(string? message = null)
    {
        if (_runningGameProcess != null && !_runningGameProcess.HasExited)
        {
            try
            {
                _runningGameProcess.Kill(true);
                _runningGameProcess.Dispose();
                
                if (!string.IsNullOrEmpty(message))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(message, "O'yin yopildi", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }
            }
            catch { }
        }
        
        _runningGameProcess = null;
        _runningGameName = null;
        IsPlaying = false;
    }
    
    private async Task HeartbeatAsync()
    {
        while (!_cts!.Token.IsCancellationRequested && IsConnected)
        {
            try
            {
                await Task.Delay(5000, _cts.Token);
                await SendMessageAsync(new NetworkMessage { Type = MessageType.Heartbeat, SenderId = "client" });
            }
            catch { break; }
        }
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
                    // Server bilan aloqa uzildi
                    Application.Current.Dispatcher.Invoke(() => Disconnect(wasForced: true));
                    break;
                }
                
                await Application.Current.Dispatcher.InvokeAsync(() => HandleMessage(message));
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(() => Disconnect(wasForced: true));
                break;
            }
        }
    }
    
    private void HandleMessage(NetworkMessage message)
    {
        switch (message.Type)
        {
            case MessageType.GameListResponse:
                var games = message.GetPayloadAs<List<GameInfo>>();
                if (games != null)
                {
                    Games.Clear();
                    foreach (var game in games)
                    {
                        Games.Add(game);
                    }
                    if (Games.Count > 0 && SelectedGame == null)
                    {
                        SelectedGame = Games[0];
                    }
                }
                break;
                
            case MessageType.FileChunk:
                var chunk = message.GetPayloadAs<FileChunk>();
                if (chunk != null)
                {
                    DownloadProgress = (double)(chunk.Offset + chunk.Data.Length) / chunk.TotalSize * 100;
                    DownloadStatus = $"{chunk.Offset + chunk.Data.Length:N0} / {chunk.TotalSize:N0} bytes";
                }
                break;
                
            // Login javobi
            case MessageType.UserLoginResponse:
                var loginResponse = message.GetPayloadAs<UserLoginResponse>();
                if (loginResponse != null)
                {
                    HandleLoginResponse(loginResponse);
                }
                break;
                
            // Admin tomonidan o'yin yopish buyrug'i
            case MessageType.ForceCloseGame:
                CloseRunningGame("Administrator tomonidan o'yin yopildi.");
                break;
                
            // Admin/Server tomonidan uzish buyrug'i (balans tugashi yoki admin)
            case MessageType.ForceDisconnect:
                var reason = message.Payload;
                _balansTimer?.Stop();
                CloseRunningGame($"Tizimdan chiqarildingiz.\n{reason}");
                ResetLoginState();
                Disconnect(wasForced: true);
                break;
        }
    }
    
    private async Task SendMessageAsync(NetworkMessage message)
    {
        if (_stream == null) return;
        var data = message.ToBytes();
        await _stream.WriteAsync(data);
        await _stream.FlushAsync();
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
    
    private void SelectGame(GameInfo? game)
    {
        if (game != null)
        {
            SelectedGame = game;
        }
    }
    
    private async void PlayGame()
    {
        if (SelectedGame == null || !IsConnected || IsPlaying) return;
        
        IsPlaying = true;
        _runningGameName = SelectedGame.Name;
        
        // O'yin ishga tushganini xabar berish
        var message = new NetworkMessage
        {
            Type = MessageType.LaunchGame,
            SenderId = "client",
            Payload = SelectedGame.Id
        };
        await SendMessageAsync(message);
        
        // O'yinni ishga tushirish (demo - Notepad)
        try
        {
            _runningGameProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "notepad.exe", // Demo uchun
                    UseShellExecute = true
                },
                EnableRaisingEvents = true
            };
            
            _runningGameProcess.Exited += async (s, e) =>
            {
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    IsPlaying = false;
                    _runningGameProcess = null;
                    
                    // Server ga o'yin yopilganini xabar berish
                    if (IsConnected)
                    {
                        await SendMessageAsync(new NetworkMessage
                        {
                            Type = MessageType.GameClosed,
                            SenderId = "client"
                        });
                    }
                });
            };
            
            _runningGameProcess.Start();
        }
        catch (Exception ex)
        {
            IsPlaying = false;
            MessageBox.Show($"O'yinni ishga tushirishda xato: {ex.Message}", "Xato",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private string GetMacAddress()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            return nic?.GetPhysicalAddress().ToString() ?? "00:00:00:00:00:00";
        }
        catch
        {
            return "00:00:00:00:00:00";
        }
    }
    
    #region Login/Session Methods
    
    private async void LoginAsync()
    {
        // Telefon raqamdan faqat raqamlarni olish
        var cleanPhone = new string(TelefonRaqam.Where(char.IsDigit).ToArray());
        
        // Parolni tozalash
        var cleanPassword = Parol.Trim();
        
        // Validatsiya
        if (cleanPhone.Length != 9)
        {
            LoginError = "Telefon raqam 9 ta raqamdan iborat bo'lishi kerak!";
            return;
        }
        
        // Faqat lotin harflari va raqamlarni tekshirish
        if (!Regex.IsMatch(cleanPassword, @"^[a-zA-Z0-9]+$"))
        {
            LoginError = "Parol faqat lotin harflari va raqamlardan iborat bo'lishi kerak!";
            return;
        }
        
        if (cleanPassword.Length < 3)
        {
            LoginError = "Parol kamida 3 ta belgidan iborat bo'lishi kerak!";
            return;
        }
        
        IsLoggingIn = true;
        LoginError = "";
        
        try
        {
            var loginRequest = new NetworkMessage
            {
                Type = MessageType.UserLogin,
                SenderId = "client"
            };
            loginRequest.SetPayload(new UserLoginRequest
            {
                TelefonRaqam = cleanPhone,
                Parol = cleanPassword
            });
            await SendMessageAsync(loginRequest);
        }
        catch (Exception ex)
        {
            LoginError = $"Xato: {ex.Message}";
            IsLoggingIn = false;
        }
    }
    
    private void HandleLoginResponse(UserLoginResponse response)
    {
        IsLoggingIn = false;
        
        if (response.Success)
        {
            _userId = response.UserId;
            UserFullName = $"{response.Familya} {response.Ism}";
            CurrentBalans = response.Balans;
            IsLoggedIn = true;
            LoginError = "";
            TelefonRaqam = "";
            Parol = "";
            
            // Session start time
            _sessionStartTime = DateTime.Now;
            
            // Timer ni ishga tushirish
            _balansTimer?.Start();
        }
        else
        {
            LoginError = response.Message;
        }
    }
    
    private void BalansTimer_Tick(object? sender, EventArgs e)
    {
        // Har minutda balansdan 100 ayirish
        CurrentBalans -= 100;
        
        // Balansni serverga yuborish
        _ = UpdateBalansAsync();
        
        // Agar balans 0 ga tushsa
        if (CurrentBalans <= 0)
        {
            CurrentBalans = 0;
            _balansTimer?.Stop();
            // Server ForceDisconnect yuboradi
        }
    }
    
    private async Task UpdateBalansAsync()
    {
        if (!IsConnected || _userId <= 0) return;
        
        var balansUpdate = new NetworkMessage
        {
            Type = MessageType.UserBalansUpdate,
            SenderId = "client"
        };
        balansUpdate.SetPayload(new BalansUpdate
        {
            UserId = _userId,
            YangiBalans = CurrentBalans,
            YechilganSumma = 100
        });
        await SendMessageAsync(balansUpdate);
    }
    
    private async void EndSession()
    {
        // Timer ni to'xtatish
        _balansTimer?.Stop();
        
        // O'yinni yopish (agar o'ynayotgan bo'lsa)
        CloseRunningGame();
        
        // Serverga logout yuborish
        if (IsConnected && _userId > 0)
        {
            await SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.UserLogout,
                SenderId = "client"
            });
        }
        
        // Reset login state
        ResetLoginState();
        
        MessageBox.Show("Seans yakunlandi. Rahmat!", "Seans", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    private void ResetLoginState()
    {
        IsLoggedIn = false;
        _userId = 0;
        UserFullName = "";
        CurrentBalans = 0;
        TelefonRaqam = "";
        Parol = "";
        _balansTimer?.Stop();
    }
    
    #endregion
    
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
}

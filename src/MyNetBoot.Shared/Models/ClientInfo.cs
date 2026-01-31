using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MyNetBoot.Shared.Models;

/// <summary>
/// Client kompyuter ma'lumotlari
/// </summary>
public class ClientInfo : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = string.Empty;
    private string _macAddress = string.Empty;
    private string _ipAddress = string.Empty;
    private ClientStatus _status = ClientStatus.Offline;
    private DateTime _lastSeen = DateTime.Now;
    private string _currentGame = string.Empty;
    private long _totalBytesReceived;
    private long _totalBytesSent;
    private string _diskImageId = string.Empty;
    private bool _isBlocked;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string MacAddress
    {
        get => _macAddress;
        set { _macAddress = value; OnPropertyChanged(); }
    }

    public string IpAddress
    {
        get => _ipAddress;
        set { _ipAddress = value; OnPropertyChanged(); }
    }

    public ClientStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public DateTime LastSeen
    {
        get => _lastSeen;
        set { _lastSeen = value; OnPropertyChanged(); }
    }

    public string CurrentGame
    {
        get => _currentGame;
        set { _currentGame = value; OnPropertyChanged(); }
    }

    public long TotalBytesReceived
    {
        get => _totalBytesReceived;
        set { _totalBytesReceived = value; OnPropertyChanged(); }
    }

    public long TotalBytesSent
    {
        get => _totalBytesSent;
        set { _totalBytesSent = value; OnPropertyChanged(); }
    }

    public string DiskImageId
    {
        get => _diskImageId;
        set { _diskImageId = value; OnPropertyChanged(); }
    }

    public bool IsBlocked
    {
        get => _isBlocked;
        set { _isBlocked = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Status matni (UI uchun)
    /// </summary>
    public string StatusText => Status switch
    {
        ClientStatus.Online => "🟢 Online",
        ClientStatus.Playing => "🎮 O'ynayapti",
        ClientStatus.Downloading => "⬇️ Yuklamoqda",
        ClientStatus.Updating => "🔄 Yangilanmoqda",
        _ => "⚫ Offline"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum ClientStatus
{
    Offline = 0,
    Online = 1,
    Playing = 2,
    Downloading = 3,
    Updating = 4
}

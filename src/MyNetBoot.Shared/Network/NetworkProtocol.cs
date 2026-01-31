using System.Text.Json;

namespace MyNetBoot.Shared.Network;

/// <summary>
/// Tarmoq xabar turlari
/// </summary>
public enum MessageType : byte
{
    // Ulanish
    Connect = 0x01,
    ConnectResponse = 0x02,
    Disconnect = 0x03,
    Heartbeat = 0x04,
    HeartbeatResponse = 0x05,

    // Client so'rovlari
    GetGameList = 0x10,
    GameListResponse = 0x11,
    GetGameInfo = 0x12,
    GameInfoResponse = 0x13,
    
    // Fayl transferi
    RequestFile = 0x20,
    FileChunk = 0x21,
    FileTransferComplete = 0x22,
    FileTransferError = 0x23,
    
    // O'yin boshqaruvi
    LaunchGame = 0x30,
    GameLaunched = 0x31,
    GameClosed = 0x32,
    ForceCloseGame = 0x33,      // Server o'yinni yopishni buyuradi
    ForceDisconnect = 0x34,     // Server clientni uzadi
    
    // Admin buyruqlari
    AdminAuth = 0x40,
    AdminAuthResponse = 0x41,
    GetClients = 0x42,
    ClientsResponse = 0x43,
    GetStats = 0x44,
    StatsResponse = 0x45,
    AddGame = 0x46,
    RemoveGame = 0x47,
    UpdateGame = 0x48,
    BlockClient = 0x49,
    UnblockClient = 0x4A,
    KickClient = 0x4B,
    SendMessage = 0x4C,
    
    // Foydalanuvchi kirish/chiqish
    UserLogin = 0x50,
    UserLoginResponse = 0x51,
    UserLogout = 0x52,
    UserBalansUpdate = 0x53,
    UserSessionEnd = 0x54,
    GetUsers = 0x55,
    UsersResponse = 0x56,
    
    // Xatolar
    Error = 0xFF
}

/// <summary>
/// Tarmoq xabari
/// </summary>
public class NetworkMessage
{
    public MessageType Type { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string Payload { get; set; } = string.Empty;
    
    public T? GetPayloadAs<T>() where T : class
    {
        if (string.IsNullOrEmpty(Payload)) return null;
        return JsonSerializer.Deserialize<T>(Payload);
    }
    
    public void SetPayload<T>(T data)
    {
        Payload = JsonSerializer.Serialize(data);
    }
    
    public byte[] ToBytes()
    {
        var json = JsonSerializer.Serialize(this);
        var data = System.Text.Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);
        var result = new byte[4 + data.Length];
        Array.Copy(lengthBytes, 0, result, 0, 4);
        Array.Copy(data, 0, result, 4, data.Length);
        return result;
    }
    
    public static NetworkMessage? FromBytes(byte[] data)
    {
        if (data.Length < 4) return null;
        var json = System.Text.Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<NetworkMessage>(json);
    }
}

/// <summary>
/// Ulanish so'rovi
/// </summary>
public class ConnectRequest
{
    public string ClientName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
}

/// <summary>
/// Ulanish javobi
/// </summary>
public class ConnectResponse
{
    public bool Success { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ServerVersion { get; set; } = "1.0.0";
}

/// <summary>
/// Fayl so'rovi
/// </summary>
public class FileRequest
{
    public string GameId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long Offset { get; set; }
    public int ChunkSize { get; set; } = 65536;
}

/// <summary>
/// Fayl bo'lagi
/// </summary>
public class FileChunk
{
    public string GameId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long Offset { get; set; }
    public long TotalSize { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public bool IsLast { get; set; }
}

/// <summary>
/// Server statistikasi
/// </summary>
public class ServerStats
{
    public int TotalClients { get; set; }
    public int OnlineClients { get; set; }
    public int PlayingClients { get; set; }
    public int TotalGames { get; set; }
    public long TotalBytesTransferred { get; set; }
    public TimeSpan Uptime { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public double NetworkBandwidth { get; set; }
}

/// <summary>
/// Foydalanuvchi kirish so'rovi
/// </summary>
public class UserLoginRequest
{
    public string TelefonRaqam { get; set; } = string.Empty;
    public string Parol { get; set; } = string.Empty;
}

/// <summary>
/// Foydalanuvchi kirish javobi
/// </summary>
public class UserLoginResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Familya { get; set; } = string.Empty;
    public string Ism { get; set; } = string.Empty;
    public decimal Balans { get; set; }
    public string Holat { get; set; } = string.Empty;
}

/// <summary>
/// Balans yangilanishi
/// </summary>
public class BalansUpdate
{
    public int UserId { get; set; }
    public decimal YangiBalans { get; set; }
    public decimal YechilganSumma { get; set; }
}


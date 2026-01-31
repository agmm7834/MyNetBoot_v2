namespace MyNetBoot.Shared.Models;

/// <summary>
/// Disk image ma'lumotlari
/// </summary>
public class DiskImage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime LastModified { get; set; } = DateTime.Now;
    public DiskImageType ImageType { get; set; } = DiskImageType.Base;
    public string BaseImageId { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int AssignedClients { get; set; }
}

public enum DiskImageType
{
    Base = 0,      // Asosiy disk image
    Game = 1,      // O'yin disk image
    Snapshot = 2   // Snapshot
}

/// <summary>
/// Server sozlamalari
/// </summary>
public class ServerSettings
{
    public int ClientPort { get; set; } = 8800;
    public int AdminPort { get; set; } = 8801;
    public string DataPath { get; set; } = "data";
    public string GamesPath { get; set; } = "data/games";
    public string ImagesPath { get; set; } = "data/images";
    public int MaxClients { get; set; } = 100;
    public int MaxConcurrentDownloads { get; set; } = 10;
    public int FileTransferBufferSize { get; set; } = 65536;
    public bool EnableLogging { get; set; } = true;
    public string LogPath { get; set; } = "logs";
}

namespace MyNetBoot.Shared.Models;

/// <summary>
/// O'yin ma'lumotlari
/// </summary>
public class GameInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string CoverImagePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Version { get; set; } = "1.0.0";
    public DateTime AddedDate { get; set; } = DateTime.Now;
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    public string Category { get; set; } = "Uncategorized";
    public bool IsEnabled { get; set; } = true;
    public int PlayCount { get; set; }
    public string[] RequiredFiles { get; set; } = Array.Empty<string>();
}

/// <summary>
/// O'yin kategoriyasi
/// </summary>
public class GameCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE7FC";
    public int GameCount { get; set; }
}

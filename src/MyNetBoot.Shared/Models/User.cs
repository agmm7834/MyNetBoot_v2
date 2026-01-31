using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MyNetBoot.Shared.Models;

/// <summary>
/// Foydalanuvchi (Mijoz) ma'lumotlari
/// </summary>
public class User : INotifyPropertyChanged
{
    private int _id;
    private string _familya = string.Empty;
    private string _ism = string.Empty;
    private string _telefonRaqam = string.Empty;
    private string _parol = string.Empty;
    private decimal _balans;
    private UserHolat _holat = UserHolat.Block;

    public int Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Familya (kamida 5 ta belgi)
    /// </summary>
    public string Familya
    {
        get => _familya;
        set { _familya = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Ism (kamida 3 ta belgi)
    /// </summary>
    public string Ism
    {
        get => _ism;
        set { _ism = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Telefon raqami (9 ta raqam, unique)
    /// </summary>
    public string TelefonRaqam
    {
        get => _telefonRaqam;
        set { _telefonRaqam = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Parol (kamida 3 ta belgi)
    /// </summary>
    public string Parol
    {
        get => _parol;
        set { _parol = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Balans (manfiy bo'lmasligi kerak)
    /// </summary>
    public decimal Balans
    {
        get => _balans;
        set { _balans = Math.Max(0, value); OnPropertyChanged(); }
    }

    /// <summary>
    /// Holat (block yoki faol, default: block)
    /// </summary>
    public UserHolat Holat
    {
        get => _holat;
        set { _holat = value; OnPropertyChanged(); OnPropertyChanged(nameof(HolatText)); }
    }

    public string HolatText => Holat == UserHolat.Faol ? "✅ Faol" : "🔒 Block";

    public string FullName => $"{Familya} {Ism}";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum UserHolat
{
    Block = 0,
    Faol = 1
}

/// <summary>
/// Foydalanuvchi seans tarixi
/// </summary>
public class UserSeans
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Familya { get; set; } = string.Empty;
    public string Ism { get; set; } = string.Empty;
    public string TelefonRaqam { get; set; } = string.Empty;
    public decimal YechilganBalans { get; set; }
    public DateTime BoshlashVaqti { get; set; }
    public DateTime TugashVaqti { get; set; }
    public int OynalganMinut { get; set; }
}

using System.Text.Json;
using MyNetBoot.Shared.Models;

namespace MyNetBoot.Server.Services;

/// <summary>
/// O'yinlar boshqaruvi xizmati
/// </summary>
public class GameService
{
    private readonly string _gamesPath;
    private readonly string _configPath;
    private List<GameInfo> _games = new();
    private List<GameCategory> _categories = new();

    public IReadOnlyList<GameInfo> Games => _games;
    public IReadOnlyList<GameCategory> Categories => _categories;

    public event EventHandler? GamesChanged;

    public GameService(string dataPath)
    {
        _gamesPath = Path.Combine(dataPath, "games");
        _configPath = Path.Combine(dataPath, "config");

        Directory.CreateDirectory(_gamesPath);
        Directory.CreateDirectory(_configPath);
    }

    public async Task LoadAsync()
    {
        var gamesFile = Path.Combine(_configPath, "games.json");
        if (File.Exists(gamesFile))
        {
            var json = await File.ReadAllTextAsync(gamesFile);
            _games = JsonSerializer.Deserialize<List<GameInfo>>(json) ?? new();
        }

        var categoriesFile = Path.Combine(_configPath, "categories.json");
        if (File.Exists(categoriesFile))
        {
            var json = await File.ReadAllTextAsync(categoriesFile);
            _categories = JsonSerializer.Deserialize<List<GameCategory>>(json) ?? new();
        }

        if (_categories.Count == 0)
        {
            _categories = new List<GameCategory>
            {
                new() { Name = "Action", Icon = "\uE7FC" },
                new() { Name = "Racing", Icon = "\uE804" },
                new() { Name = "Sports", Icon = "\uE8D6" },
                new() { Name = "Strategy", Icon = "\uE74C" },
                new() { Name = "Arcade", Icon = "\uE7FC" }
            };
            await SaveAsync();
        }

        Console.WriteLine($"[GAMES] {_games.Count} ta o'yin yuklandi");
    }

    public async Task SaveAsync()
    {
        var gamesFile = Path.Combine(_configPath, "games.json");
        var json = JsonSerializer.Serialize(_games, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(gamesFile, json);

        var categoriesFile = Path.Combine(_configPath, "categories.json");
        json = JsonSerializer.Serialize(_categories, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(categoriesFile, json);
    }

    public async Task<GameInfo> AddGameAsync(GameInfo game)
    {
        game.Id = Guid.NewGuid().ToString();
        game.AddedDate = DateTime.Now;
        game.LastUpdated = DateTime.Now;

        // O'yin papkasini yaratish
        var gamePath = Path.Combine(_gamesPath, game.Id);
        Directory.CreateDirectory(gamePath);
        game.ExecutablePath = gamePath;

        _games.Add(game);
        await SaveAsync();
        GamesChanged?.Invoke(this, EventArgs.Empty);

        Console.WriteLine($"[GAMES] Qo'shildi: {game.Name}");
        return game;
    }

    public async Task UpdateGameAsync(GameInfo game)
    {
        var index = _games.FindIndex(g => g.Id == game.Id);
        if (index >= 0)
        {
            game.LastUpdated = DateTime.Now;
            _games[index] = game;
            await SaveAsync();
            GamesChanged?.Invoke(this, EventArgs.Empty);
            Console.WriteLine($"[GAMES] Yangilandi: {game.Name}");
        }
    }

    public async Task RemoveGameAsync(string gameId)
    {
        var game = _games.FirstOrDefault(g => g.Id == gameId);
        if (game != null)
        {
            _games.Remove(game);

            // O'yin fayllarini o'chirish
            var gamePath = Path.Combine(_gamesPath, gameId);
            if (Directory.Exists(gamePath))
            {
                Directory.Delete(gamePath, true);
            }

            await SaveAsync();
            GamesChanged?.Invoke(this, EventArgs.Empty);
            Console.WriteLine($"[GAMES] O'chirildi: {game.Name}");
        }
    }

    public GameInfo? GetGame(string gameId)
    {
        return _games.FirstOrDefault(g => g.Id == gameId);
    }

    public string GetGamePath(string gameId)
    {
        return Path.Combine(_gamesPath, gameId);
    }

    public async Task<long> CalculateGameSizeAsync(string gameId)
    {
        var gamePath = GetGamePath(gameId);
        if (!Directory.Exists(gamePath)) return 0;

        long size = 0;
        await Task.Run(() =>
        {
            foreach (var file in Directory.GetFiles(gamePath, "*", SearchOption.AllDirectories))
            {
                size += new FileInfo(file).Length;
            }
        });
        return size;
    }
}

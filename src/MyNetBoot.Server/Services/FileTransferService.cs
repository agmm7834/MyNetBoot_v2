using MyNetBoot.Shared.Network;

namespace MyNetBoot.Server.Services;

/// <summary>
/// Fayl uzatish xizmati
/// </summary>
public class FileTransferService
{
    private readonly GameService _gameService;
    private readonly int _bufferSize;

    public FileTransferService(GameService gameService, int bufferSize = 65536)
    {
        _gameService = gameService;
        _bufferSize = bufferSize;
    }

    public async Task<FileChunk?> GetFileChunkAsync(FileRequest request)
    {
        var gamePath = _gameService.GetGamePath(request.GameId);
        var filePath = Path.Combine(gamePath, request.FilePath);

        if (!File.Exists(filePath))
        {
            return null;
        }

        var fileInfo = new FileInfo(filePath);
        var chunkSize = Math.Min(request.ChunkSize, _bufferSize);

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(request.Offset, SeekOrigin.Begin);

        var buffer = new byte[chunkSize];
        var bytesRead = await stream.ReadAsync(buffer);

        if (bytesRead < chunkSize)
        {
            Array.Resize(ref buffer, bytesRead);
        }

        return new FileChunk
        {
            GameId = request.GameId,
            FilePath = request.FilePath,
            Offset = request.Offset,
            TotalSize = fileInfo.Length,
            Data = buffer,
            IsLast = request.Offset + bytesRead >= fileInfo.Length
        };
    }

    public async Task<string[]> GetGameFilesAsync(string gameId)
    {
        var gamePath = _gameService.GetGamePath(gameId);
        if (!Directory.Exists(gamePath))
        {
            return Array.Empty<string>();
        }

        return await Task.Run(() =>
        {
            var files = Directory.GetFiles(gamePath, "*", SearchOption.AllDirectories);
            return files.Select(f => Path.GetRelativePath(gamePath, f)).ToArray();
        });
    }

    public async Task<Dictionary<string, long>> GetGameFileSizesAsync(string gameId)
    {
        var gamePath = _gameService.GetGamePath(gameId);
        if (!Directory.Exists(gamePath))
        {
            return new Dictionary<string, long>();
        }

        return await Task.Run(() =>
        {
            var result = new Dictionary<string, long>();
            var files = Directory.GetFiles(gamePath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(gamePath, file);
                result[relativePath] = new FileInfo(file).Length;
            }
            return result;
        });
    }
}

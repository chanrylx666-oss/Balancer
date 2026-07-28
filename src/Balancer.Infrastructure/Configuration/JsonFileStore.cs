using System.Text.Json;

namespace Balancer.Infrastructure.Configuration;

public sealed class JsonFileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        Directory.CreateDirectory(directory!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken);
    }

    public async Task<T?> LoadAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return default;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken);
    }
}

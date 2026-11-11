using System.Text;
using System.Text.Json;

namespace Sentinel.Core;

public static class EventStore
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "SentinelDFIR");
    public static string EventsPath => Path.Combine(DataDirectory, "events.jsonl");

    public static async Task AppendAsync(SecurityEvent item, CancellationToken ct = default)
    {
        await WriteLock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(DataDirectory);
            await using var stream = new FileStream(EventsPath, FileMode.Append,
                FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteLineAsync(JsonSerializer.Serialize(item).AsMemory(), ct);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static List<SecurityEvent> ReadLatest(int count = 500)
    {
        var items = new Queue<SecurityEvent>();
        if (!File.Exists(EventsPath)) return [];
        using var stream = new FileStream(EventsPath, FileMode.Open,
            FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            try
            {
                var evt = JsonSerializer.Deserialize<SecurityEvent>(line);
                if (evt is null) continue;
                items.Enqueue(evt);
                while (items.Count > count) items.Dequeue();
            }
            catch (JsonException) { /* Skip a truncated last line. */ }
        }
        return items.Reverse().ToList();
    }
}

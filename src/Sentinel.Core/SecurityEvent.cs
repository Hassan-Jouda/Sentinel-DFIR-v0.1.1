namespace Sentinel.Core;

public sealed class SecurityEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = "Agent";
    public string Category { get; set; } = "System";
    public string Severity { get; set; } = "Info";
    public int RiskScore { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int? ProcessId { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

using System.Diagnostics.Eventing.Reader;
using System.Xml.Linq;
using Sentinel.Core;

namespace Sentinel.Agent;

public sealed class SysmonListener : IDisposable
{
    private EventLogWatcher? _watcher;
    private readonly Action<SecurityEvent> _publish;
    private const string Channel = "Microsoft-Windows-Sysmon/Operational";

    public SysmonListener(Action<SecurityEvent> publish) => _publish = publish;

    public bool Start()
    {
        try
        {
            var query = new EventLogQuery(Channel, PathType.LogName,
                "*[System[(EventID=1 or EventID=3 or EventID=11 or EventID=12 or EventID=13 or EventID=14 or EventID=22)]]");
            _watcher = new EventLogWatcher(query);
            _watcher.EventRecordWritten += OnEvent;
            _watcher.Enabled = true;
            return true;
        }
        catch (EventLogException)
        {
            Dispose();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            Dispose();
            return false;
        }
    }

    private void OnEvent(object? sender, EventRecordWrittenEventArgs args)
    {
        if (args.EventException is not null || args.EventRecord is null) return;
        try
        {
            using var record = args.EventRecord;
            var xml = XDocument.Parse(record.ToXml());
            var attributes = xml.Descendants()
                .Where(node => node.Name.LocalName == "Data")
                .Where(node => node.Attribute("Name") is not null)
                .GroupBy(node => (string)node.Attribute("Name")!)
                .ToDictionary(group => group.Key,
                    group => group.First().Value, StringComparer.OrdinalIgnoreCase);

            var id = record.Id;
            var category = id switch
            {
                1 => "Process",
                3 or 22 => "Network",
                11 => "File",
                12 or 13 or 14 => "Registry",
                _ => "System"
            };
            var title = id switch
            {
                1 => "Process created",
                3 => "Network connection",
                11 => "File created",
                12 or 13 or 14 => "Registry change",
                22 => "DNS query",
                _ => $"Sysmon event {id}"
            };
            var evt = new SecurityEvent
            {
                Source = "Sysmon",
                Category = category,
                Title = title,
                TimestampUtc = record.TimeCreated is DateTime time
                    ? new DateTimeOffset(time.ToUniversalTime())
                    : DateTimeOffset.UtcNow,
                ProcessId = attributes.TryGetValue("ProcessId", out var pid) && int.TryParse(pid, out var result)
                    ? result : null,
                Attributes = attributes
            };
            ThreatScorer.Apply(evt);
            _publish(evt);
        }
        catch
        {
            // Event listener must never crash the worker service on a malformed event.
        }
    }

    public void Dispose()
    {
        if (_watcher is null) return;
        _watcher.EventRecordWritten -= OnEvent;
        _watcher.Dispose();
        _watcher = null;
    }
}

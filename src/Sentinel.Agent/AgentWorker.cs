using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentinel.Core;

namespace Sentinel.Agent;

public sealed class AgentWorker : BackgroundService
{
    private readonly ILogger<AgentWorker> _logger;
    public AgentWorker(ILogger<AgentWorker> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // This is a V0.1 lab prototype: no automated termination or quarantine.
        var queue = Channel.CreateBounded<SecurityEvent>(new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        void Publish(SecurityEvent e) => queue.Writer.TryWrite(e);
        var storeTask = Task.Run(async () =>
        {
            await foreach (var item in queue.Reader.ReadAllAsync())
            {
                try { await EventStore.AppendAsync(item); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to store event"); }
            }
        }, CancellationToken.None);

        using var sysmon = new SysmonListener(Publish);
        var sysmonEnabled = sysmon.Start();
        Publish(new SecurityEvent
        {
            Category = "Health", Title = "Agent started",
            Description = sysmonEnabled
                ? "Sysmon event subscription active."
                : "Sysmon unavailable: install/configure it for rich process, registry, file and network telemetry."
        });

        HashSet<string>? knownProcesses = null;
        HashSet<string>? knownConnections = null;
        var lastHealth = DateTimeOffset.MinValue;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            do
            {
                var processes = GetProcesses();
                if (knownProcesses is not null)
                    foreach (var item in processes.Except(knownProcesses).Take(1000))
                        Publish(new SecurityEvent
                        {
                            Category = "Process", Title = "Process observed by polling",
                            Description = item,
                            Source = "ProcessPoller"
                        });
                knownProcesses = processes;

                var connections = GetTcpConnections();
                if (knownConnections is not null)
                    foreach (var item in connections.Except(knownConnections).Take(1000))
                        Publish(new SecurityEvent
                        {
                            Category = "Network", Title = "New established TCP connection",
                            Description = item + " (PID attribution requires Sysmon)",
                            Source = "TcpPoller"
                        });
                knownConnections = connections;

                if (DateTimeOffset.UtcNow - lastHealth >= TimeSpan.FromSeconds(30))
                {
                    Publish(new SecurityEvent
                    {
                        Category = "Health", Title = "Agent heartbeat",
                        Description = $"Process sample: {processes.Count}; established TCP: {connections.Count}; Sysmon: {sysmonEnabled}"
                    });
                    lastHealth = DateTimeOffset.UtcNow;
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            queue.Writer.TryComplete();
            await storeTask;
            _logger.LogInformation("Sentinel Agent stopped.");
        }
    }

    private static HashSet<string> GetProcesses()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    var start = p.StartTime.ToUniversalTime().Ticks;
                    result.Add($"{p.Id} | {p.ProcessName} | {start}");
                }
                catch
                {
                    try { result.Add($"{p.Id} | {p.ProcessName}"); }
                    catch { /* Protected or transient process */ }
                }
            }
        }
        return result;
    }

    private static HashSet<string> GetTcpConnections()
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections()
                .Where(x => x.State == TcpState.Established)
                .Select(x => $"{x.LocalEndPoint} => {x.RemoteEndPoint}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch { return new HashSet<string>(); }
    }
}

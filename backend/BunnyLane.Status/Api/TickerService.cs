using BunnyLane.Status.Jellyfin;
using BunnyLane.Status.Metrics;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Api;

public sealed record Tick(double? WanInBps, double? WanOutBps, double? VpsUsedBytes, long VpsCapBytes, int? Streams);

/// <summary>Computes the header numbers once per interval and fans them out to every open ticker stream.</summary>
public sealed class TickerService(IPrometheus prom, IJellyfin jellyfin, IOptions<StatusOptions> options, TimeProvider clock, ILogger<TickerService> log)
    : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private readonly VpsUsage _usage = new(prom);
    private readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
    private readonly object _lock = new();
    private Tick? _latest;
    private TaskCompletionSource<Tick> _next = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Every tick from now on, starting with the latest one if there is one.</summary>
    public async IAsyncEnumerable<Tick> SubscribeAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        Task<Tick> next;
        lock (_lock)
        {
            if (_latest is { } latest) yield return latest;
            next = _next.Task;
        }
        while (!ct.IsCancellationRequested)
        {
            var tick = await next.WaitAsync(ct);
            yield return tick;
            lock (_lock) next = _next.Task;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                Publish(await ComputeAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                log.LogWarning(e, "ticker computation failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private void Publish(Tick tick)
    {
        TaskCompletionSource<Tick> done;
        lock (_lock)
        {
            _latest = tick;
            done = _next;
            _next = new TaskCompletionSource<Tick>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        done.TrySetResult(tick);
    }

    private async Task<Tick> ComputeAsync(CancellationToken ct)
    {
        double? wanIn = null, wanOut = null, used = null;
        int? streams = null;
        try
        {
            wanIn = (await prom.QueryAsync(Promql.WanRx, ct)).FirstOrDefault()?.Value;
            wanOut = (await prom.QueryAsync(Promql.WanTx, ct)).FirstOrDefault()?.Value;
            var now = clock.GetUtcNow();
            used = (await _usage.ForAsync(Months.Current(now, _tz), now, ct)).Total;
        }
        catch (MetricsUnavailableException) { }
        try
        {
            streams = (await jellyfin.GetSessionsAsync(ct)).Count(s => s.IsPlaying);
        }
        catch (JellyfinUnavailableException) { }
        return new Tick(wanIn, wanOut, used, options.Value.VpsCapBytes, streams);
    }
}

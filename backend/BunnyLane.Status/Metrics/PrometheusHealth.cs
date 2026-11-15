namespace BunnyLane.Status.Metrics;

/// <summary>Readiness: the app is ready when Prometheus answered within the last minute.</summary>
public sealed class PrometheusHealth(TimeProvider clock)
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(60);
    private long _lastSuccessTicks = long.MinValue;

    public void MarkSuccess() => Interlocked.Exchange(ref _lastSuccessTicks, clock.GetUtcNow().UtcTicks);

    public bool IsReady()
    {
        var last = Interlocked.Read(ref _lastSuccessTicks);
        return last != long.MinValue && clock.GetUtcNow().UtcTicks - last <= Window.Ticks;
    }
}

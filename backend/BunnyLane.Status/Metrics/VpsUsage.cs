namespace BunnyLane.Status.Metrics;

public sealed record MonthUsage(Month Month, double InBytes, double OutBytes, double[] PerDayBytes)
{
    public double Total => InBytes + OutBytes;
}

/// <summary>Raised when the VPS counters have no samples at all for the window.</summary>
public sealed class NoVpsSamplesException() : MetricsUnavailableException("no vps counter samples");

/// <summary>Month-to-date VPS traffic from the raw node_exporter counters, shared by <c>vps</c> and the ticker.</summary>
public sealed class VpsUsage(IPrometheus prom)
{
    public static readonly TimeSpan Step = TimeSpan.FromHours(1);

    public async Task<MonthUsage> ForAsync(Month month, DateTimeOffset now, CancellationToken ct)
    {
        var to = month.End < now ? month.End : now;
        var rx = (await prom.QueryRangeAsync(Promql.VpsRx, month.Start, to, Step, ct)).FirstOrDefault()?.Values ?? [];
        var tx = (await prom.QueryRangeAsync(Promql.VpsTx, month.Start, to, Step, ct)).FirstOrDefault()?.Values ?? [];
        if (rx.All(v => v is null) && tx.All(v => v is null)) throw new NoVpsSamplesException();
        var perDay = Counters.IncreasePerBucket(rx, 24).Zip(Counters.IncreasePerBucket(tx, 24), (a, b) => a + b).ToArray();
        return new MonthUsage(month, Counters.Increase(rx), Counters.Increase(tx), perDay);
    }
}

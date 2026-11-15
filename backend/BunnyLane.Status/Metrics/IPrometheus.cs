namespace BunnyLane.Status.Metrics;

public sealed record Instant(IReadOnlyDictionary<string, string> Labels, double Value);

/// <param name="Timestamps">The full step grid from <c>from</c> to <c>to</c>; <paramref name="Values"/> is null where Prometheus had no sample.</param>
public sealed record RangeSeries(IReadOnlyDictionary<string, string> Labels, long[] Timestamps, double?[] Values);

/// <summary>Raised on timeout, transport or HTTP failure. Handlers map it to <c>[UNKNOWN]</c>, never to a 500.</summary>
public sealed class MetricsUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public interface IPrometheus
{
    Task<Instant[]> QueryAsync(string promql, CancellationToken ct);
    Task<RangeSeries[]> QueryRangeAsync(string promql, DateTimeOffset from, DateTimeOffset to, TimeSpan step, CancellationToken ct);
}

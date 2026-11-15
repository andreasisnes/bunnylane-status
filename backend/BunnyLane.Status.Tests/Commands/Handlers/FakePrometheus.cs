using BunnyLane.Status.Metrics;

namespace BunnyLane.Status.Tests.Commands.Handlers;

/// <summary>Canned answers keyed by PromQL. Unknown queries return empty; <see cref="Down"/> makes everything throw.</summary>
public sealed class FakePrometheus : IPrometheus
{
    private readonly Dictionary<string, Instant[]> _instant = new();
    private readonly Dictionary<string, RangeSeries[]> _range = new();
    public List<(string Promql, TimeSpan Step, DateTimeOffset From, DateTimeOffset To)> RangeCalls { get; } = [];
    public int Calls { get; private set; }
    public bool Down { get; set; }

    public FakePrometheus Set(string promql, params Instant[] values) { _instant[promql] = values; return this; }
    public FakePrometheus Set(string promql, double value) => Set(promql, I(value));
    public FakePrometheus SetRange(string promql, params RangeSeries[] series) { _range[promql] = series; return this; }

    public static Instant I(double value, params (string, string)[] labels) =>
        new(labels.ToDictionary(l => l.Item1, l => l.Item2), value);

    public Task<Instant[]> QueryAsync(string promql, CancellationToken ct)
    {
        Calls++;
        if (Down) throw new MetricsUnavailableException("down");
        return Task.FromResult(_instant.GetValueOrDefault(promql, []));
    }

    public Task<RangeSeries[]> QueryRangeAsync(string promql, DateTimeOffset from, DateTimeOffset to, TimeSpan step, CancellationToken ct)
    {
        Calls++;
        RangeCalls.Add((promql, step, from, to));
        if (Down) throw new MetricsUnavailableException("down");
        if (_range.TryGetValue(promql, out var s)) return Task.FromResult(s);
        var n = (int)((to - from) / step) + 1;
        var grid = Enumerable.Range(0, n).Select(i => from.ToUnixTimeSeconds() + (long)(i * step.TotalSeconds)).ToArray();
        return Task.FromResult<RangeSeries[]>([new RangeSeries(new Dictionary<string, string>(), grid, grid.Select(_ => (double?)1.0).ToArray())]);
    }

    /// <summary>A six node cluster with real-looking hostnames and IPs that must never leak.</summary>
    public static FakePrometheus Cluster()
    {
        var f = new FakePrometheus();
        string[] cps = ["k8s-01-cp01", "k8s-02-cp01", "k8s-03-cp01"];
        string[] ws = ["k8s-01-w01", "k8s-02-w01", "k8s-03-w01"];
        string[] ips = ["192.168.30.111", "192.168.30.121", "192.168.30.131", "192.168.30.114", "192.168.30.124", "192.168.30.134"];
        var all = cps.Concat(ws).ToArray();
        f.Set(Promql.NodeReady, all.Select(n => I(1, ("node", n))).ToArray());
        f.Set(Promql.NodeCount, 6);
        f.Set(Promql.NodeRole, cps.Select(n => I(1, ("node", n), ("role", "control-plane"))).ToArray());
        f.Set(Promql.NodeNames, all.Select((n, i) => I(1, ("nodename", n), ("instance", ips[i] + ":9100"))).ToArray());
        f.Set(Promql.NodeCpu, all.Select((_, i) => I(0.1 * (i + 1), ("instance", ips[i] + ":9100"))).ToArray());
        f.Set(Promql.NodeMem, all.Select((_, i) => I(0.5, ("instance", ips[i] + ":9100"))).ToArray());
        f.Set(Promql.NodeLoad, all.Select((_, i) => I(1.5, ("instance", ips[i] + ":9100"))).ToArray());
        f.Set(Promql.NodeUptime, all.Select((_, i) => I(273600, ("instance", ips[i] + ":9100"))).ToArray());
        f.Set(Promql.NetRx, ws.Select((_, i) => I(1e6, ("instance", ips[3 + i] + ":9100"))).ToArray());
        f.Set(Promql.NetTx, ws.Select((_, i) => I(2e6, ("instance", ips[3 + i] + ":9100"))).ToArray());
        f.Set(Promql.WanRx, 184e6);
        f.Set(Promql.WanTx, 20e6);
        f.Set(Promql.LonghornCapacity, ws.Select(n => I(512e9, ("node", n))).ToArray());
        f.Set(Promql.LonghornUsage, ws.Select(n => I(200e9, ("node", n))).ToArray());
        f.Set(Promql.LonghornVolumeRobustness, [I(1, ("volume", "pvc-1"), ("pvc", "servarr/sonarr")), I(2, ("volume", "pvc-2"), ("pvc", "jellyfin/config"))]);
        f.Set(Promql.LonghornDegraded, 1);
        f.Set(Promql.NasUsed, 6.8e12);
        f.Set(Promql.NasCapacity, 37e12);
        f.Set(Promql.GpuBusy, ws.Select((_, i) => I(42, ("instance", ips[3 + i] + ":9100"), ("card", "card1"))).ToArray());
        f.Set(Promql.GpuVramUsed, ws.Select((_, i) => I(1e9, ("instance", ips[3 + i] + ":9100"))).ToArray());
        f.Set(Promql.GpuVramSize, ws.Select((_, i) => I(4e9, ("instance", ips[3 + i] + ":9100"))).ToArray());
        f.Set(Promql.GpuTemp, ws.Select((_, i) => I(61, ("instance", ips[3 + i] + ":9100"))).ToArray());
        f.Set(Promql.VpsUp, 1);
        f.Set(Promql.DeploymentsUnavailable, [I(2, ("deployment", "paabrygga-api"))]);
        f.Set(Promql.FluxNotReady, 0);
        return f;
    }
}

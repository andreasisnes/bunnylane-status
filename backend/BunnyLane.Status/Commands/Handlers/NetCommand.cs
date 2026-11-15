using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;

namespace BunnyLane.Status.Commands.Handlers;

public sealed class NetCommand(IPrometheus prom, TimeProvider? clock = null) : MetricsCommand(prom, clock ?? TimeProvider.System)
{
    public override string Name => "net";

    protected override async Task<CommandResult> RunCoreAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var window = args.Count > 0 ? args[0] : "1h";
        var wanIn = Single(await Prom.QueryAsync(Promql.WanRx, ct));
        var wanOut = Single(await Prom.QueryAsync(Promql.WanTx, ct));
        var chart = await ChartAsync($"net {window}", "bit/s", [("in", Promql.WanRx), ("out", Promql.WanTx)], window, ct);

        var map = await NodeMap.LoadAsync(Prom, ct);
        var rx = map.ByInstance(await Prom.QueryAsync(Promql.NetRx, ct));
        var tx = map.ByInstance(await Prom.QueryAsync(Promql.NetTx, ct));
        var rows = map.Workers.OrderBy(w => w.Alias, StringComparer.Ordinal)
            .Select(w => new[] { w.Alias, Format.Bits(rx.GetValueOrDefault(w.Alias)), Format.Bits(tx.GetValueOrDefault(w.Alias)) }).ToArray();

        var inSeries = chart.Series[0].Values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return CommandResult.Ok(Name,
            new TextBlock($"wan in {Format.Bits(wanIn)} · out {Format.Bits(wanOut)}", Tone.Amber),
            new SparklineBlock($"in, last {window}", inSeries, "bit/s"),
            chart,
            new TableBlock(["node", "in", "out"], rows));
    }
}

using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;

namespace BunnyLane.Status.Commands.Handlers;

public sealed class GpuCommand(IPrometheus prom, TimeProvider? clock = null) : MetricsCommand(prom, clock ?? TimeProvider.System)
{
    public override string Name => "gpu";

    protected override async Task<CommandResult> RunCoreAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var window = args.Count > 0 ? args[0] : "1h";
        var busyRaw = await Prom.QueryAsync(Promql.GpuBusy, ct);
        if (busyRaw.Length == 0) return Unavailable();

        var map = await NodeMap.LoadAsync(Prom, ct);
        var busy = map.ByInstance(busyRaw);
        var vramUsed = map.ByInstance(await Prom.QueryAsync(Promql.GpuVramUsed, ct));
        var vramSize = map.ByInstance(await Prom.QueryAsync(Promql.GpuVramSize, ct));
        var temp = map.ByInstance(await Prom.QueryAsync(Promql.GpuTemp, ct));

        var rows = map.Workers.OrderBy(w => w.Alias, StringComparer.Ordinal).Select(w => new[]
        {
            w.Alias,
            Format.Percent(busy.GetValueOrDefault(w.Alias) / 100),
            $"{Format.Bytes(vramUsed.GetValueOrDefault(w.Alias))} / {Format.Bytes(vramSize.GetValueOrDefault(w.Alias))}",
            $"{Math.Round(temp.GetValueOrDefault(w.Alias))} °C",
        }).ToArray();

        var chart = await ChartAsync($"gpu {window}", "%", [("busy", $"avg({Promql.GpuBusy})")], window, ct);
        return CommandResult.Ok(Name, new TableBlock(["node", "busy", "vram", "temp"], rows), chart);
    }
}

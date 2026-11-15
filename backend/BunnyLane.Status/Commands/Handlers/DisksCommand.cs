using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;

namespace BunnyLane.Status.Commands.Handlers;

public sealed class DisksCommand(IPrometheus prom, TimeProvider? clock = null) : MetricsCommand(prom, clock ?? TimeProvider.System)
{
    public override string Name => "disks";

    protected override async Task<CommandResult> RunCoreAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var map = await NodeMap.LoadAsync(Prom, ct);
        var capacityRaw = await Prom.QueryAsync(Promql.LonghornCapacity, ct);
        if (capacityRaw.Length == 0) return Unavailable();
        var capacity = map.ByHost(capacityRaw);
        var usage = map.ByHost(await Prom.QueryAsync(Promql.LonghornUsage, ct));
        var robustness = await Prom.QueryAsync(Promql.LonghornVolumeRobustness, ct);

        var blocks = new List<Block>();
        foreach (var w in map.Workers.OrderBy(w => w.Alias, StringComparer.Ordinal))
        {
            var cap = capacity.GetValueOrDefault(w.Alias);
            var used = usage.GetValueOrDefault(w.Alias);
            var fraction = cap > 0 ? used / cap : 0;
            blocks.Add(new BarBlock(w.Alias, Math.Clamp(fraction, 0, 1), $"{Format.Bytes(used)} used · {Format.Bytes(Math.Max(0, cap - used))} free · {Format.Percent(fraction)}"));
        }

        var volumes = robustness.Length;
        var degraded = robustness.Count(v => v.Value >= 2);
        blocks.Add(new TagsBlock([
            new Tag($"{volumes} volumes", Level.Ok),
            degraded > 0
                ? new Tag($"{degraded} volume{(degraded == 1 ? "" : "s")} degraded", Level.Degraded)
                : new Tag("all replicas healthy", Level.Ok),
        ]));
        return CommandResult.Ok(Name, blocks.ToArray());
    }
}

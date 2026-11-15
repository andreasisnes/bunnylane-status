using System.Globalization;
using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;

namespace BunnyLane.Status.Commands.Handlers;

public sealed class NodesCommand(IPrometheus prom, TimeProvider? clock = null) : MetricsCommand(prom, clock ?? TimeProvider.System)
{
    public override string Name => "nodes";

    protected override async Task<CommandResult> RunCoreAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var map = await NodeMap.LoadAsync(Prom, ct);
        var cpu = map.ByInstance(await Prom.QueryAsync(Promql.NodeCpu, ct));
        var mem = map.ByInstance(await Prom.QueryAsync(Promql.NodeMem, ct));
        var load = map.ByInstance(await Prom.QueryAsync(Promql.NodeLoad, ct));
        var up = map.ByInstance(await Prom.QueryAsync(Promql.NodeUptime, ct));

        var rows = map.Nodes.OrderBy(n => n.IsControlPlane ? 0 : 1).ThenBy(n => n.Alias, StringComparer.Ordinal).Select(n => new[]
        {
            n.Alias,
            n.IsControlPlane ? "control plane" : "worker",
            Format.Percent(cpu.GetValueOrDefault(n.Alias)),
            Format.Percent(mem.GetValueOrDefault(n.Alias)),
            load.GetValueOrDefault(n.Alias).ToString("F2", CultureInfo.InvariantCulture),
            Format.Duration(TimeSpan.FromSeconds(up.GetValueOrDefault(n.Alias))),
        }).ToArray();

        return CommandResult.Ok(Name, new TableBlock(["node", "role", "cpu", "memory", "load", "up"], rows));
    }
}

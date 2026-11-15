using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;

namespace BunnyLane.Status.Commands.Handlers;

public sealed class StatusCommand(IPrometheus prom, TimeProvider? clock = null) : MetricsCommand(prom, clock ?? TimeProvider.System)
{
    public override string Name => "status";

    protected override async Task<CommandResult> RunCoreAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var tags = new List<Tag>
        {
            await Try(ct, async t =>
            {
                var ready = (await Prom.QueryAsync(Promql.NodeReady, t)).Count(v => v.Value == 1);
                var total = Single(await Prom.QueryAsync(Promql.NodeCount, t)) is { } n ? (int)n : throw new MetricsUnavailableException("no node count");
                return new Tag($"nodes {ready}/{total}", ready == total ? Level.Ok : ready == 0 ? Level.Down : Level.Degraded);
            }, "nodes"),
            await Try(ct, async t =>
            {
                var degraded = Single(await Prom.QueryAsync(Promql.LonghornDegraded, t)) ?? throw new MetricsUnavailableException("no longhorn metrics");
                return new Tag("storage", degraded > 0 ? Level.Degraded : Level.Ok);
            }, "storage"),
            await Try(ct, async t =>
            {
                var unavailable = (Single(await Prom.QueryAsync(Promql.DeploymentsUnavailable, t)) ?? throw new MetricsUnavailableException("no kube-state-metrics"))
                                  + (Single(await Prom.QueryAsync(Promql.FluxNotReady, t)) ?? 0);
                return unavailable > 0
                    ? new Tag($"{(int)unavailable} workloads", Level.Degraded)
                    : new Tag("workloads", Level.Ok);
            }, "workloads"),
            await Try(ct, async t =>
                new Tag("edge", (Single(await Prom.QueryAsync(Promql.VpsUp, t)) ?? 0) >= 1 ? Level.Ok : Level.Down), "edge"),
        };

        var status = tags.Any(t => t.Level == Level.Unknown) ? ResultStatus.Unknown : ResultStatus.Ok;
        return new CommandResult(Name, [new TagsBlock(tags)], status);
    }

    private static async Task<Tag> Try(CancellationToken ct, Func<CancellationToken, Task<Tag>> probe, string label)
    {
        try { return await probe(ct); }
        catch (MetricsUnavailableException) { return new Tag(label, Level.Unknown); }
    }
}

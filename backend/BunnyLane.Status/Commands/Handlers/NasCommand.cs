using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;

namespace BunnyLane.Status.Commands.Handlers;

public sealed class NasCommand(IPrometheus prom, TimeProvider? clock = null) : MetricsCommand(prom, clock ?? TimeProvider.System)
{
    public override string Name => "nas";

    protected override async Task<CommandResult> RunCoreAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        if (Single(await Prom.QueryAsync(Promql.NasUsed, ct)) is not { } used || Single(await Prom.QueryAsync(Promql.NasCapacity, ct)) is not { } total)
            return Unavailable();
        var fraction = total > 0 ? used / total : 0;

        var to = Clock.GetUtcNow();
        var week = await Prom.QueryRangeAsync(Promql.NasUsed, to - TimeSpan.FromDays(7), to, TimeSpan.FromDays(1), ct);
        var samples = week.FirstOrDefault()?.Values.Where(v => v.HasValue).Select(v => v!.Value).ToArray() ?? [];
        var growthPerDay = samples.Length >= 2 ? (samples[^1] - samples[0]) / (samples.Length - 1) : 0;

        return CommandResult.Ok(Name,
            new BarBlock("media", Math.Clamp(fraction, 0, 1), $"{Format.Bytes(used)} of {Format.Bytes(total)} · {Format.Bytes(Math.Max(0, total - used))} free", Thresholds.LevelFor(fraction)),
            new TextBlock($"growth {Format.Bytes(Math.Max(0, growthPerDay))}/day over the last week", Tone.Muted));
    }
}

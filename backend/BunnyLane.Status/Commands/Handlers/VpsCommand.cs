using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Commands.Handlers;

/// <summary>This month's VPS traffic against the FlokiNet cap, both directions summed.</summary>
public sealed class VpsCommand(IPrometheus prom, IOptions<StatusOptions> options, TimeProvider? clock = null)
    : MetricsCommand(prom, clock ?? TimeProvider.System)
{
    private static readonly TimeSpan Step = TimeSpan.FromHours(1);
    private readonly StatusOptions _options = options.Value;

    public override string Name => "vps";

    protected override Task<CommandResult> RunCoreAsync(IReadOnlyList<string> args, CancellationToken ct) =>
        args.Count > 0 && args[0] == "month" ? HistoryAsync(ct) : CurrentAsync(ct);

    private async Task<CommandResult> CurrentAsync(CancellationToken ct)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var now = Clock.GetUtcNow();
        var month = Months.Current(now, tz);
        var (rx, tx) = await CountersAsync(month.Start, now, ct);

        var inBytes = Counters.Increase(rx);
        var outBytes = Counters.Increase(tx);
        var used = inBytes + outBytes;
        var cap = (double)_options.VpsCapBytes;
        var perDay = Counters.IncreasePerBucket(rx, 24).Zip(Counters.IncreasePerBucket(tx, 24), (a, b) => a + b).ToArray();
        var today = perDay.Length > 0 ? perDay[^1] : 0;
        var daysToReset = (int)Math.Ceiling((month.End - now).TotalDays);
        var projected = Months.Project(used, month.Start, month.End, now);

        return CommandResult.Ok(Name,
            new BarBlock($"vps {month.Label}", Math.Clamp(used / cap, 0, 1), $"{Format.Bytes(used)} of {Format.Bytes(cap)} · {Format.Percent(used / cap)}"),
            new TextBlock($"in {Format.Bytes(inBytes)} · out {Format.Bytes(outBytes)} · today {Format.Bytes(today)}", Tone.Plain),
            new TextBlock($"projected {Format.Bytes(projected)} at month end · {daysToReset} days to reset", projected > cap ? Tone.Amber : Tone.Muted),
            new SparklineBlock("GB per day", perDay.Select(b => b / 1e9).ToArray(), "GB"));
    }

    private async Task<CommandResult> HistoryAsync(CancellationToken ct)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var now = Clock.GetUtcNow();
        var rows = new List<string[]>();
        foreach (var month in Months.Last(6, now, tz))
        {
            var (rx, tx) = await CountersAsync(month.Start, month.End < now ? month.End : now, ct);
            var inBytes = Counters.Increase(rx);
            var outBytes = Counters.Increase(tx);
            rows.Add([month.Label, Format.Bytes(inBytes), Format.Bytes(outBytes), Format.Bytes(inBytes + outBytes)]);
        }
        return CommandResult.Ok(Name, new TableBlock(["month", "in", "out", "total"], rows.ToArray()));
    }

    private async Task<(double?[] Rx, double?[] Tx)> CountersAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var rx = await Prom.QueryRangeAsync(Promql.VpsRx, from, to, Step, ct);
        var tx = await Prom.QueryRangeAsync(Promql.VpsTx, from, to, Step, ct);
        return (rx.FirstOrDefault()?.Values ?? [], tx.FirstOrDefault()?.Values ?? []);
    }
}

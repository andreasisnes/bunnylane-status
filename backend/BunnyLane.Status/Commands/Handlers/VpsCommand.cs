using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Commands.Handlers;

/// <summary>This month's VPS traffic against the FlokiNet cap, both directions summed.</summary>
public sealed class VpsCommand(IPrometheus prom, IOptions<StatusOptions> options, TimeProvider? clock = null)
    : MetricsCommand(prom, clock ?? TimeProvider.System)
{
    private readonly StatusOptions _options = options.Value;
    private readonly VpsUsage _usage = new(prom);

    public override string Name => "vps";

    protected override Task<CommandResult> RunCoreAsync(IReadOnlyList<string> args, CancellationToken ct) =>
        args.Count > 0 && args[0] == "month" ? HistoryAsync(ct) : CurrentAsync(ct);

    private async Task<CommandResult> CurrentAsync(CancellationToken ct)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var now = Clock.GetUtcNow();
        var month = Months.Current(now, tz);
        var usage = await _usage.ForAsync(month, now, ct);

        var cap = (double)_options.VpsCapBytes;
        var today = usage.PerDayBytes.Length > 0 ? usage.PerDayBytes[^1] : 0;
        var daysToReset = (int)Math.Ceiling((month.End - now).TotalDays);
        var projected = Months.Project(usage.Total, month.Start, month.End, now);

        return CommandResult.Ok(Name,
            new BarBlock($"vps {month.Label}", Math.Clamp(usage.Total / cap, 0, 1), $"{Format.Bytes(usage.Total)} of {Format.Bytes(cap)} · {Format.Percent(usage.Total / cap)}"),
            new TextBlock($"in {Format.Bytes(usage.InBytes)} · out {Format.Bytes(usage.OutBytes)} · today {Format.Bytes(today)}", Tone.Plain),
            new TextBlock($"projected {Format.Bytes(projected)} at month end · {daysToReset} days to reset", projected > cap ? Tone.Amber : Tone.Muted),
            new SparklineBlock("GB per day", usage.PerDayBytes.Select(b => b / 1e9).ToArray(), "GB"));
    }

    private async Task<CommandResult> HistoryAsync(CancellationToken ct)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var now = Clock.GetUtcNow();
        var rows = new List<string[]>();
        foreach (var month in Months.Last(6, now, tz))
        {
            var u = await _usage.ForAsync(month, now, ct);
            rows.Add([month.Label, Format.Bytes(u.InBytes), Format.Bytes(u.OutBytes), Format.Bytes(u.Total)]);
        }
        return CommandResult.Ok(Name, new TableBlock(["month", "in", "out", "total"], rows.ToArray()));
    }
}

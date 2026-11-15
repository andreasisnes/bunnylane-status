using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;

namespace BunnyLane.Status.Commands.Handlers;

/// <summary>Base for commands backed by Prometheus: any <see cref="MetricsUnavailableException"/> becomes an <c>[UNKNOWN]</c> result.</summary>
public abstract class MetricsCommand(IPrometheus prom, TimeProvider clock) : ICommandHandler
{
    protected IPrometheus Prom { get; } = prom;
    protected TimeProvider Clock { get; } = clock;

    public abstract string Name { get; }

    public async Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        try
        {
            return await RunCoreAsync(args, ct);
        }
        catch (MetricsUnavailableException)
        {
            return Unavailable();
        }
    }

    protected abstract Task<CommandResult> RunCoreAsync(IReadOnlyList<string> args, CancellationToken ct);

    protected CommandResult Unavailable() =>
        CommandResult.Unknown(Name, new TagsBlock([new Tag(Name, Level.Unknown)]), new TextBlock("metrics unavailable right now", Tone.Muted));

    protected static double Single(Instant[] values) => values.Length == 0 ? 0 : values[0].Value;

    /// <summary>Range windows: the span and the step from the plan (1h→30s, 24h→5m, 7d→30m, 30d→2h).</summary>
    protected static (TimeSpan Span, TimeSpan Step) Window(string arg) => arg switch
    {
        "24h" => (TimeSpan.FromHours(24), TimeSpan.FromMinutes(5)),
        "7d" => (TimeSpan.FromDays(7), TimeSpan.FromMinutes(30)),
        "30d" => (TimeSpan.FromDays(30), TimeSpan.FromHours(2)),
        _ => (TimeSpan.FromHours(1), TimeSpan.FromSeconds(30)),
    };

    protected async Task<ChartBlock> ChartAsync(string title, string unit, (string Name, string Promql)[] series, string arg, CancellationToken ct)
    {
        var (span, step) = Window(arg);
        var to = Clock.GetUtcNow();
        var from = to - span;
        var fetched = new List<Series>();
        long[] timestamps = [];
        foreach (var (name, promql) in series)
        {
            var result = await Prom.QueryRangeAsync(promql, from, to, step, ct);
            var first = result.FirstOrDefault();
            if (first is not null) timestamps = first.Timestamps;
            fetched.Add(new Series(name, first?.Values ?? []));
        }
        return new ChartBlock(title, unit, timestamps, fetched);
    }
}

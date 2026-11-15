using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using BunnyLane.Status.Commands;
using BunnyLane.Status.Jellyfin;
using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Api;

public sealed record Tick(double? WanInBps, double? WanOutBps, double? VpsUsedBytes, long VpsCapBytes, int? Streams);

public static class StreamEndpoints
{
    public static readonly TimeSpan WatchInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan TickerInterval = TimeSpan.FromSeconds(5);

    public static IEndpointRouteBuilder MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/watch", (string? line, CommandRunner runner, TimeProvider clock, CancellationToken ct) =>
            TypedResults.ServerSentEvents(Watch(line ?? "", runner, clock, ct)))
            .RequireRateLimiting(RateLimit.Streams);

        app.MapGet("/api/ticker", (IPrometheus prom, IJellyfin jellyfin, IOptions<StatusOptions> options, TimeProvider clock, CancellationToken ct) =>
            TypedResults.ServerSentEvents(Ticker(prom, jellyfin, options.Value, clock, ct)))
            .RequireRateLimiting(RateLimit.Streams);
        return app;
    }

    private static async IAsyncEnumerable<SseItem<object>> Watch(string line, CommandRunner runner, TimeProvider clock, [EnumeratorCancellation] CancellationToken ct)
    {
        var target = CommandRunner.Parse(line) switch
        {
            ParseResult.Ok { Command.Name: "watch" } ok => new ParsedCommand(ok.Command.Args[0], ok.Command.Args.Skip(1).ToList()),
            ParseResult.Ok ok => ok.Command,
            _ => null,
        };
        if (target is null)
        {
            yield return new SseItem<object>(CommandRunner.NotFound(line.Length > CommandLine.MaxEcho ? line[..CommandLine.MaxEcho] : line), "error");
            yield break;
        }

        while (!ct.IsCancellationRequested)
        {
            yield return new SseItem<object>(await runner.RunAsync(target, ct), "result");
            await Task.Delay(WatchInterval, clock, ct);
        }
    }

    private static async IAsyncEnumerable<SseItem<Tick>> Ticker(IPrometheus prom, IJellyfin jellyfin, StatusOptions options, TimeProvider clock, [EnumeratorCancellation] CancellationToken ct)
    {
        var usage = new VpsUsage(prom);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
        while (!ct.IsCancellationRequested)
        {
            double? wanIn = null, wanOut = null, used = null;
            int? streams = null;
            try
            {
                wanIn = (await prom.QueryAsync(Promql.WanRx, ct)).FirstOrDefault()?.Value;
                wanOut = (await prom.QueryAsync(Promql.WanTx, ct)).FirstOrDefault()?.Value;
                var now = clock.GetUtcNow();
                used = (await usage.ForAsync(Months.Current(now, tz), now, ct)).Total;
            }
            catch (MetricsUnavailableException) { }
            try
            {
                streams = (await jellyfin.GetSessionsAsync(ct)).Count(s => s.IsPlaying);
            }
            catch (JellyfinUnavailableException) { }

            yield return new SseItem<Tick>(new Tick(wanIn, wanOut, used, options.VpsCapBytes, streams), "tick");
            await Task.Delay(TickerInterval, clock, ct);
        }
    }
}

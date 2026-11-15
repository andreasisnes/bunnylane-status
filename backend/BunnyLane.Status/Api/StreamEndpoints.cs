using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using BunnyLane.Status.Commands;

namespace BunnyLane.Status.Api;

public static class StreamEndpoints
{
    public static readonly TimeSpan WatchInterval = TimeSpan.FromSeconds(2);

    public static IEndpointRouteBuilder MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/watch", (string? line, CommandRunner runner, TimeProvider clock, CancellationToken ct) =>
            TypedResults.ServerSentEvents(Watch(line ?? "", runner, clock, ct)))
            .RequireRateLimiting(RateLimit.Streams);

        app.MapGet("/api/ticker", (TickerService ticker, CancellationToken ct) =>
            TypedResults.ServerSentEvents(Ticks(ticker, ct)))
            .RequireRateLimiting(RateLimit.Ticker);
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

    private static async IAsyncEnumerable<SseItem<Tick>> Ticks(TickerService ticker, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var tick in ticker.SubscribeAsync(ct)) yield return new SseItem<Tick>(tick, "tick");
    }
}

using System.Net;
using System.Text.Json;
using BunnyLane.Status.Render;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Api;

public sealed class WatchStreamTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<HttpResponseMessage> Open(HttpClient client, string path, CancellationToken ct) =>
        await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);

    [Fact]
    public async Task Watch_yields_two_results_within_five_seconds()
    {
        using var factory = new ApiFactory();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await Open(factory.CreateClient(), "/api/watch?line=status", cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        var events = await ApiFactory.ReadEventsAsync(await response.Content.ReadAsStreamAsync(cts.Token), 2, cts.Token);

        events.Should().HaveCount(2).And.OnlyContain(e => e.Event == "result");
        JsonSerializer.Deserialize<CommandResult>(events[0].Data, Json)!.Command.Should().Be("status");
    }

    [Fact]
    public async Task Disallowed_line_yields_one_error_event_and_closes()
    {
        using var factory = new ApiFactory();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await Open(factory.CreateClient(), "/api/watch?line=rm%20-rf", cts.Token);

        var events = await ApiFactory.ReadEventsAsync(await response.Content.ReadAsStreamAsync(cts.Token), 2, cts.Token);

        events.Should().ContainSingle().Which.Event.Should().Be("error");
    }

    [Fact]
    public async Task Prometheus_outage_mid_stream_degrades_to_unknown_and_keeps_streaming()
    {
        using var factory = new ApiFactory();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var response = await Open(factory.CreateClient(), "/api/watch?line=status", cts.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token));

        var first = await ApiFactory.ReadEventsAsync(reader, 1, cts.Token);
        factory.Prom.Down = true;
        var next = await ApiFactory.ReadEventsAsync(reader, 1, cts.Token);

        JsonSerializer.Deserialize<CommandResult>(first[0].Data, Json)!.Status.Should().Be(ResultStatus.Ok);
        JsonSerializer.Deserialize<CommandResult>(next[0].Data, Json)!.Status.Should().Be(ResultStatus.Unknown);
    }

    [Fact]
    public async Task Fifth_concurrent_stream_from_one_client_is_rejected()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var open = new List<HttpResponseMessage>();
        for (var i = 0; i < 4; i++) open.Add(await Open(client, "/api/watch?line=help", cts.Token));

        using var fifth = await Open(client, "/api/watch?line=help", cts.Token);

        fifth.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        open.ForEach(r => r.Dispose());
    }

    [Fact]
    public async Task Client_disconnect_stops_the_handler()
    {
        using var factory = new ApiFactory();
        using var cts = new CancellationTokenSource();
        var response = await Open(factory.CreateClient(), "/api/watch?line=status", cts.Token);
        await ApiFactory.ReadEventsAsync(await response.Content.ReadAsStreamAsync(cts.Token), 1, cts.Token);

        cts.Cancel();
        response.Dispose();
        await Task.Delay(500);
        var calls = factory.Prom.Calls;
        await Task.Delay(2500);

        factory.Prom.Calls.Should().Be(calls);
    }

    [Fact]
    public async Task Ticker_sends_a_tick_with_the_header_numbers()
    {
        using var factory = new ApiFactory();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await Open(factory.CreateClient(), "/api/ticker", cts.Token);

        var events = await ApiFactory.ReadEventsAsync(await response.Content.ReadAsStreamAsync(cts.Token), 1, cts.Token);

        events.Single().Event.Should().Be("tick");
        using var tick = JsonDocument.Parse(events.Single().Data);
        tick.RootElement.GetProperty("wanInBps").GetDouble().Should().Be(184e6);
        tick.RootElement.GetProperty("vpsCapBytes").GetInt64().Should().Be(6_000_000_000_000);
        tick.RootElement.GetProperty("streams").GetInt32().Should().Be(1);
        tick.RootElement.TryGetProperty("vpsUsedBytes", out _).Should().BeTrue();
    }
}

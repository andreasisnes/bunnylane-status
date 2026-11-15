using System.Text.Json;
using BunnyLane.Status.Commands.Handlers;
using BunnyLane.Status.Jellyfin;
using BunnyLane.Status.Render;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Commands.Handlers;

public sealed class FakeJellyfin(params Session[] sessions) : IJellyfin
{
    public bool Down { get; set; }

    public Task<IReadOnlyList<Session>> GetSessionsAsync(CancellationToken ct) =>
        Down ? throw new JellyfinUnavailableException("down") : Task.FromResult<IReadOnlyList<Session>>(sessions);
}

public class NowCommandTests
{
    private static readonly Session Anna = new("u1", "anna", "84.212.10.5", true, 8_000_000, true);
    private static readonly Session Bo = new("u2", "bo", "192.168.30.50", true, 30_000_000, false);
    private static readonly Session Idle = new("u3", "carl", "10.0.0.9", false, 0, false);

    [Fact]
    public async Task Now_counts_streams_bitrate_and_gpu_use_without_names()
    {
        var result = await new NowCommand(new FakeJellyfin(Anna, Bo, Idle)).RunAsync([], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Blocks.OfType<TextBlock>().First().Text.Should().Be("2 streams · 38.0 Mbit/s · gpu in use");
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().NotContainAny("anna", "bo\"", "carl", "84.212", "192.168");
    }

    [Fact]
    public async Task Now_with_nothing_playing_says_so()
    {
        var result = await new NowCommand(new FakeJellyfin(Idle)).RunAsync([], CancellationToken.None);

        result.Blocks.OfType<TextBlock>().First().Text.Should().Be("0 streams · 0 bit/s · gpu idle");
        result.Blocks.OfType<TagsBlock>().Single().Tags.Should().ContainEquivalentOf(new Tag("gpu idle", Level.Ok));
    }

    [Fact]
    public async Task Now_is_unknown_when_jellyfin_is_down()
    {
        var result = await new NowCommand(new FakeJellyfin { Down = true }).RunAsync([], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Unknown);
    }
}

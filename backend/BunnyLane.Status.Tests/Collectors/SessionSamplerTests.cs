using BunnyLane.Status.Collectors;
using BunnyLane.Status.Jellyfin;
using BunnyLane.Status.Tests.Commands.Handlers;
using BunnyLane.Status.Tests.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Tests.Collectors;

public sealed class SessionSamplerTests : IDisposable
{
    private readonly TestDb _db = new();
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-15T20:00:00Z");

    private SessionSampler Sampler(params Session[] sessions) =>
        new(new FakeJellyfin(sessions), _db, Options.Create(new StatusOptions()), new FixedClock(Now), NullLogger<SessionSampler>.Instance);

    [Fact]
    public async Task Adds_thirty_seconds_for_playing_sessions_only_and_leases_their_ip()
    {
        var sampler = Sampler(
            new Session("u1", "anna", "84.212.10.5", true, 8_000_000, true),
            new Session("u3", "carl", "10.0.0.9", false, 0, false));

        await sampler.SampleOnceAsync(CancellationToken.None);
        await sampler.SampleOnceAsync(CancellationToken.None);

        await using var db = _db.CreateDbContext();
        db.WatchTime.Should().ContainSingle().Which.Should().BeEquivalentTo(new { UserId = "u1", DisplayName = "anna", Seconds = 60, Date = new DateOnly(2026, 11, 15) });
        db.Leases.Should().ContainSingle().Which.Should().BeEquivalentTo(new { RemoteIp = "84.212.10.5", UserId = "u1", SeenUtc = Now });
    }

    [Fact]
    public async Task Jellyfin_outage_is_skipped_quietly()
    {
        var sampler = new SessionSampler(new FakeJellyfin { Down = true }, _db, Options.Create(new StatusOptions()), new FixedClock(Now), NullLogger<SessionSampler>.Instance);

        await sampler.Invoking(s => s.SampleOnceAsync(CancellationToken.None)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Log_follower_credits_bytes_from_access_lines()
    {
        await Sampler(new Session("u1", "anna", "84.212.10.5", true, 8_000_000, true)).SampleOnceAsync(CancellationToken.None);
        var follower = new TraefikLogFollower(new FakeLogSource(
            """{"ClientAddr":"84.212.10.5:1","DownstreamContentSize":1000,"RequestHost":"jellyfin.bunnylane.no","time":"2026-11-15T21:00:01+01:00"}""",
            """{"ClientAddr":"8.8.8.8:1","DownstreamContentSize":500,"RequestHost":"jellyfin.bunnylane.no","time":"2026-11-15T21:00:02+01:00"}""",
            """{"ClientAddr":"192.168.30.5:1","DownstreamContentSize":9999,"RequestHost":"jellyfin.bunnylane.no","time":"2026-11-15T21:00:03+01:00"}""",
            "garbage"), _db, Options.Create(new StatusOptions()), new FixedClock(Now), NullLogger<TraefikLogFollower>.Instance);

        await follower.FollowOnceAsync(CancellationToken.None);

        await using var db = _db.CreateDbContext();
        db.VpsBytes.Single(v => v.UserId == "u1").Bytes.Should().Be(1000);
        db.VpsBytes.Single(v => v.UserId == "other").Bytes.Should().Be(500);
        db.VpsBytes.Should().HaveCount(2);
        follower.Unparsable.Should().Be(1);
    }

    public void Dispose() => _db.Dispose();
}

public sealed class FakeLogSource(params string[] lines) : ILogSource
{
    public async IAsyncEnumerable<string> ReadLinesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var line in lines)
        {
            await Task.Yield();
            yield return line;
        }
    }
}

using BunnyLane.Status.Data;
using BunnyLane.Status.Jellyfin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Collectors;

/// <summary>Every 30 s: credit each playing session with 30 s of watch time and remember which IP it plays from.</summary>
public sealed class SessionSampler(
    IJellyfin jellyfin, IDbContextFactory<StatusDb> dbFactory, IOptions<StatusOptions> options, TimeProvider clock, ILogger<SessionSampler> log)
    : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SampleOnceAsync(stoppingToken);
            if (clock.GetUtcNow().Minute == 0 && clock.GetUtcNow().Second < 30) await PruneAsync(stoppingToken);
        }
    }

    public async Task SampleOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<Session> sessions;
        try
        {
            sessions = await jellyfin.GetSessionsAsync(ct);
        }
        catch (JellyfinUnavailableException e)
        {
            log.LogDebug(e, "jellyfin unavailable, skipping sample");
            return;
        }

        var now = clock.GetUtcNow();
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, _tz).DateTime);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        foreach (var s in sessions.Where(s => s.IsPlaying && s.UserId.Length > 0))
        {
            await db.AddWatchTimeAsync(day, s.UserId, s.DisplayName, (long)Interval.TotalSeconds, ct);
            if (s.RemoteEndPoint.Length > 0)
                await db.UpsertLeaseAsync(new SessionLease { RemoteIp = s.RemoteEndPoint, UserId = s.UserId, DisplayName = s.DisplayName, SeenUtc = now }, ct);
        }
    }

    private async Task PruneAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.PruneAsync(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), _tz).DateTime), ct);
    }
}

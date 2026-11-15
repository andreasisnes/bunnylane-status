using BunnyLane.Status.Data;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Data;

public sealed class StatusDbTests : IDisposable
{
    private readonly TestDb _db = new();

    [Fact]
    public async Task Prune_drops_rows_older_than_thirteen_months()
    {
        var today = new DateOnly(2026, 11, 15);
        await using (var db = _db.CreateDbContext())
        {
            db.WatchTime.AddRange(
                new WatchTimeDaily { Date = today.AddMonths(-14), UserId = "u1", DisplayName = "anna", Seconds = 30 },
                new WatchTimeDaily { Date = today.AddMonths(-12), UserId = "u1", DisplayName = "anna", Seconds = 30 });
            db.VpsBytes.AddRange(
                new VpsBytesDaily { Date = today.AddMonths(-14), UserId = "other", DisplayName = "", Bytes = 1 },
                new VpsBytesDaily { Date = today, UserId = "other", DisplayName = "", Bytes = 1 });
            await db.SaveChangesAsync();
        }

        await using (var db = _db.CreateDbContext())
        {
            await db.PruneAsync(today, CancellationToken.None);
        }

        await using var check = _db.CreateDbContext();
        check.WatchTime.Should().ContainSingle(w => w.Date == today.AddMonths(-12));
        check.VpsBytes.Should().ContainSingle(v => v.Date == today);
    }

    [Fact]
    public async Task Upserts_accumulate_per_user_per_day()
    {
        var day = new DateOnly(2026, 11, 15);
        await using (var db = _db.CreateDbContext())
        {
            await db.AddWatchTimeAsync(day, "u1", "anna", 30, CancellationToken.None);
            await db.AddWatchTimeAsync(day, "u1", "anna", 30, CancellationToken.None);
            await db.AddVpsBytesAsync(day, "u1", "anna", 1000, CancellationToken.None);
            await db.AddVpsBytesAsync(day, "u1", "anna", 500, CancellationToken.None);
        }

        await using var check = _db.CreateDbContext();
        check.WatchTime.Single().Seconds.Should().Be(60);
        check.VpsBytes.Single().Bytes.Should().Be(1500);
    }

    public void Dispose() => _db.Dispose();
}

using BunnyLane.Status.Commands.Handlers;
using BunnyLane.Status.Data;
using BunnyLane.Status.Render;
using BunnyLane.Status.Tests.Data;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Tests.Commands.Handlers;

public sealed class TopCommandTests : IDisposable
{
    private readonly TestDb _db = new();

    private TopCommand Cmd(DateTimeOffset now, params string[] hidden) =>
        new(_db, Options.Create(new StatusOptions { HiddenUsers = hidden }), new FixedClock(now));

    private async Task Seed()
    {
        await using var db = _db.CreateDbContext();
        var nov = new DateOnly(2026, 11, 10);
        db.WatchTime.AddRange(
            new WatchTimeDaily { Date = nov, UserId = "u1", DisplayName = "anna", Seconds = 7200 },
            new WatchTimeDaily { Date = nov, UserId = "u2", DisplayName = "bo", Seconds = 3600 },
            new WatchTimeDaily { Date = nov, UserId = "u9", DisplayName = "", Seconds = 99999 },
            new WatchTimeDaily { Date = nov, UserId = "u3", DisplayName = "hidden-person", Seconds = 88888 });
        db.VpsBytes.AddRange(
            new VpsBytesDaily { Date = nov, UserId = "u1", DisplayName = "anna", Bytes = 2_000_000_000 },
            new VpsBytesDaily { Date = nov, UserId = "u3", DisplayName = "hidden-person", Bytes = 5_000_000_000 },
            new VpsBytesDaily { Date = nov, UserId = "other", DisplayName = "", Bytes = 1_000_000_000 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Top_ranks_by_watch_time_and_hides_hidden_and_nameless_users_but_keeps_their_bytes_in_the_total()
    {
        await Seed();

        var result = await Cmd(DateTimeOffset.Parse("2026-11-15T12:00:00Z"), "hidden-person").RunAsync([], CancellationToken.None);

        var table = result.Blocks.OfType<TableBlock>().Single();
        table.Columns.Should().Equal("#", "who", "watched", "vps");
        table.Rows.Select(r => r[1]).Should().Equal("anna", "bo", "(other)");
        table.Rows[0].Should().Equal("1", "anna", "2h 0m", "2.00 GB");
        table.Rows[^1][3].Should().Be("1.00 GB");
        result.Blocks.OfType<TextBlock>().Single().Text.Should().Contain("8.00 GB");
    }

    [Fact]
    public async Task Top_starts_a_fresh_month_at_midnight_oslo_while_all_keeps_history()
    {
        await Seed();
        var dec1 = DateTimeOffset.Parse("2026-11-30T23:10:00Z"); // 00:10 on Dec 1 in Oslo

        var month = await Cmd(dec1).RunAsync([], CancellationToken.None);
        var all = await Cmd(dec1).RunAsync(["all"], CancellationToken.None);

        month.Blocks.OfType<TableBlock>().Single().Rows.Should().BeEmpty();
        all.Blocks.OfType<TableBlock>().Single().Rows.Select(r => r[1]).Should().Contain("anna");
    }

    public void Dispose() => _db.Dispose();
}

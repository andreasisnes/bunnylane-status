using BunnyLane.Status.Collectors;
using BunnyLane.Status.Data;
using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Commands.Handlers;

/// <summary>Leaderboard: watch time and VPS bytes per display name. Hidden and nameless users are left out of the rows but stay in the total.</summary>
public sealed class TopCommand(IDbContextFactory<StatusDb> dbFactory, IOptions<StatusOptions> options, TimeProvider? clock = null) : ICommandHandler
{
    private readonly StatusOptions _options = options.Value;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public string Name => "top";

    public async Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var all = args.Count > 0 && args[0] == "all";
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var now = _clock.GetUtcNow();
        var month = Months.Current(now, tz);
        var from = all ? DateOnly.MinValue : DateOnly.FromDateTime(month.Start.DateTime);
        var hidden = _options.HiddenUsers.ToHashSet(StringComparer.OrdinalIgnoreCase);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var watch = await db.WatchTime.Where(w => w.Date >= from).GroupBy(w => w.UserId)
            .Select(g => new { UserId = g.Key, Name = g.Max(w => w.DisplayName), Seconds = g.Sum(w => w.Seconds) }).ToListAsync(ct);
        var bytes = await db.VpsBytes.Where(v => v.Date >= from).GroupBy(v => v.UserId)
            .Select(g => new { UserId = g.Key, Name = g.Max(v => v.DisplayName), Bytes = g.Sum(v => v.Bytes) }).ToListAsync(ct);

        var byteMap = bytes.ToDictionary(b => b.UserId, b => b.Bytes);
        var rows = watch
            .Where(w => w.UserId != Attribution.Other && !string.IsNullOrWhiteSpace(w.Name) && !hidden.Contains(w.Name))
            .OrderByDescending(w => w.Seconds).ThenBy(w => w.Name, StringComparer.Ordinal)
            .Select((w, i) => new[] { (i + 1).ToString(), w.Name, Format.Duration(TimeSpan.FromSeconds(w.Seconds)), Format.Bytes(byteMap.GetValueOrDefault(w.UserId)) })
            .ToList();
        if (byteMap.TryGetValue(Attribution.Other, out var other) && other > 0)
            rows.Add(["", "(other)", "", Format.Bytes(other)]);

        var total = bytes.Sum(b => b.Bytes);
        var label = all ? "all time" : month.Label;
        return CommandResult.Ok(Name,
            new TableBlock(["#", "who", "watched", "vps"], rows.ToArray()),
            new TextBlock($"{label} · vps total {Format.Bytes(total)} (approx.)", Tone.Muted));
    }
}

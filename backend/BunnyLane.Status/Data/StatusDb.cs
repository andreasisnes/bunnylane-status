using Microsoft.EntityFrameworkCore;

namespace BunnyLane.Status.Data;

/// <summary>Seconds of playback per user per local day. Only the display name is kept, never what was watched.</summary>
public sealed class WatchTimeDaily
{
    public DateOnly Date { get; set; }
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long Seconds { get; set; }
}

/// <summary>Bytes that crossed the VPS per user per local day; <c>UserId == "other"</c> is unattributed.</summary>
public sealed class VpsBytesDaily
{
    public DateOnly Date { get; set; }
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long Bytes { get; set; }
}

/// <summary>Which user was last seen streaming from which public IP; the join key for access-log attribution.</summary>
public sealed record SessionLease
{
    public string RemoteIp { get; init; } = "";
    public string UserId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public DateTimeOffset SeenUtc { get; init; }
}

public sealed class StatusDb(DbContextOptions<StatusDb> options) : DbContext(options)
{
    public const int RetentionMonths = 13;

    public DbSet<WatchTimeDaily> WatchTime => Set<WatchTimeDaily>();
    public DbSet<VpsBytesDaily> VpsBytes => Set<VpsBytesDaily>();
    public DbSet<SessionLease> Leases => Set<SessionLease>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WatchTimeDaily>().HasKey(w => new { w.Date, w.UserId });
        b.Entity<VpsBytesDaily>().HasKey(v => new { v.Date, v.UserId });
        b.Entity<SessionLease>().HasKey(l => l.RemoteIp);
        // SQLite cannot order or compare DateTimeOffset text; store UTC ticks so prune and lease lookups translate.
        b.Entity<SessionLease>().Property(l => l.SeenUtc)
            .HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));
    }

    public async Task AddWatchTimeAsync(DateOnly day, string userId, string displayName, long seconds, CancellationToken ct)
    {
        var row = await WatchTime.FindAsync([day, userId], ct);
        if (row is null) WatchTime.Add(new WatchTimeDaily { Date = day, UserId = userId, DisplayName = displayName, Seconds = seconds });
        else { row.Seconds += seconds; row.DisplayName = displayName; }
        await SaveChangesAsync(ct);
    }

    public async Task AddVpsBytesAsync(DateOnly day, string userId, string displayName, long bytes, CancellationToken ct)
    {
        var row = await VpsBytes.FindAsync([day, userId], ct);
        if (row is null) VpsBytes.Add(new VpsBytesDaily { Date = day, UserId = userId, DisplayName = displayName, Bytes = bytes });
        else { row.Bytes += bytes; if (displayName.Length > 0) row.DisplayName = displayName; }
        await SaveChangesAsync(ct);
    }

    public async Task UpsertLeaseAsync(SessionLease lease, CancellationToken ct)
    {
        var existing = await Leases.FindAsync([lease.RemoteIp], ct);
        if (existing is null) Leases.Add(lease);
        else Entry(existing).CurrentValues.SetValues(lease);
        await SaveChangesAsync(ct);
    }

    /// <summary>A lease is only useful for attribution for 10 minutes; an IP is never kept longer than this.</summary>
    public static readonly TimeSpan LeaseRetention = TimeSpan.FromHours(1);

    public async Task PruneAsync(DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = today.AddMonths(-RetentionMonths);
        await WatchTime.Where(w => w.Date < cutoff).ExecuteDeleteAsync(ct);
        await VpsBytes.Where(v => v.Date < cutoff).ExecuteDeleteAsync(ct);
        var leaseCutoff = now - LeaseRetention;
        await Leases.Where(l => l.SeenUtc < leaseCutoff).ExecuteDeleteAsync(ct);
    }
}

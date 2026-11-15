using System.Runtime.CompilerServices;
using BunnyLane.Status.Data;
using k8s;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Collectors;

/// <summary>A stream of log lines. Production follows Traefik's pods through the Kubernetes API; tests feed lines directly.</summary>
public interface ILogSource
{
    IAsyncEnumerable<string> ReadLinesAsync(CancellationToken ct);
}

/// <summary>
/// Follows Traefik's JSON access log and credits bytes that crossed the VPS to the user streaming from that IP.
/// Lines are filtered in memory first (host, public IP); only candidates are buffered and written in one batch.
/// </summary>
public sealed class TraefikLogFollower(
    ILogSource source, IDbContextFactory<StatusDb> dbFactory, IOptions<StatusOptions> options, TimeProvider clock, ILogger<TraefikLogFollower> log)
    : BackgroundService
{
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);
    public const int FlushAtLines = 500;

    private readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
    private readonly List<TraefikAccessLine> _pending = [];
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private int _unparsable;

    public int Unparsable => _unparsable;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var flushTimer = new PeriodicTimer(FlushInterval, clock);
        var flusher = Task.Run(async () =>
        {
            while (await flushTimer.WaitForNextTickAsync(stoppingToken)) await FlushAsync(stoppingToken);
        }, stoppingToken);

        var backoff = TimeSpan.FromSeconds(2);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FollowOnceAsync(stoppingToken);
                backoff = TimeSpan.FromSeconds(2);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                log.LogWarning(e, "traefik log stream failed, reconnecting in {Backoff}", backoff);
            }
            try { await Task.Delay(backoff, clock, stoppingToken); } catch (OperationCanceledException) { break; }
            backoff = TimeSpan.FromSeconds(Math.Min(60, backoff.TotalSeconds * 2));
        }
        try { await flusher; } catch (OperationCanceledException) { }
    }

    /// <summary>Consumes the source until it ends, then flushes. Small gaps at pod rotation are accepted ("approx.").</summary>
    public async Task FollowOnceAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var raw in source.ReadLinesAsync(ct))
            {
                var line = TraefikAccessLine.TryParse(raw);
                if (line is null)
                {
                    Interlocked.Increment(ref _unparsable);
                    continue;
                }
                if (!Attribution.IsCandidate(line)) continue;
                int count;
                lock (_pending)
                {
                    _pending.Add(line);
                    count = _pending.Count;
                }
                if (count >= FlushAtLines) await FlushAsync(ct);
            }
        }
        finally
        {
            await FlushAsync(CancellationToken.None);
        }
    }

    /// <summary>One context, one lease lookup, one SaveChanges for everything buffered since the last flush.</summary>
    public async Task FlushAsync(CancellationToken ct)
    {
        List<TraefikAccessLine> batch;
        lock (_pending)
        {
            if (_pending.Count == 0) return;
            batch = [.. _pending];
            _pending.Clear();
        }
        await _flushGate.WaitAsync(ct);
        try
        {
            var now = clock.GetUtcNow();
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, _tz).DateTime);
            var fresh = now - Attribution.LeaseWindow;
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var leases = await db.Leases.Where(l => l.SeenUtc >= fresh).ToListAsync(ct);

            var totals = new Dictionary<string, (string Name, long Bytes)>();
            foreach (var line in batch)
            {
                var credit = Attribution.Attribute(line, leases, now);
                if (credit is null || credit.Bytes <= 0) continue;
                var (prevName, prevBytes) = totals.GetValueOrDefault(credit.UserId, (credit.DisplayName, 0L));
                totals[credit.UserId] = (credit.DisplayName.Length > 0 ? credit.DisplayName : prevName, prevBytes + credit.Bytes);
            }

            foreach (var (userId, (name, bytes)) in totals)
            {
                var row = await db.VpsBytes.FindAsync([day, userId], ct);
                if (row is null) db.VpsBytes.Add(new VpsBytesDaily { Date = day, UserId = userId, DisplayName = name, Bytes = bytes });
                else { row.Bytes += bytes; if (name.Length > 0) row.DisplayName = name; }
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogWarning(e, "could not write {Count} access-log credits; they are dropped", batch.Count);
        }
        finally
        {
            _flushGate.Release();
        }
    }
}

/// <summary>Streams <c>kubectl logs -f</c> of every Traefik pod in the configured namespace, merged into one line stream.</summary>
public sealed class KubernetesLogSource(IKubernetes k8s, IOptions<StatusOptions> options, ILogger<KubernetesLogSource> log) : ILogSource
{
    private const string Selector = "app.kubernetes.io/name=traefik";

    public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var ns = options.Value.TraefikNamespace;
        var pods = await k8s.CoreV1.ListNamespacedPodAsync(ns, labelSelector: Selector, cancellationToken: ct);
        var names = pods.Items.Select(p => p.Metadata.Name).ToList();
        if (names.Count == 0) throw new InvalidOperationException($"no traefik pods in {ns}");
        log.LogInformation("following access logs of {Count} traefik pod(s)", names.Count);

        var channel = System.Threading.Channels.Channel.CreateBounded<string>(4096);
        var readers = names.Select(name => PumpAsync(ns, name, channel.Writer, ct)).ToList();
        _ = Task.WhenAll(readers).ContinueWith(t =>
        {
            if (t.IsFaulted) log.LogWarning(t.Exception, "a traefik log stream ended with an error");
            channel.Writer.TryComplete();
        }, CancellationToken.None);

        await foreach (var line in channel.Reader.ReadAllAsync(ct)) yield return line;
    }

    private async Task PumpAsync(string ns, string pod, System.Threading.Channels.ChannelWriter<string> writer, CancellationToken ct)
    {
        using var stream = await k8s.CoreV1.ReadNamespacedPodLogAsync(ns, pod, container: "traefik", follow: true, sinceSeconds: 60, cancellationToken: ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is { } line) await writer.WriteAsync(line, ct);
    }
}

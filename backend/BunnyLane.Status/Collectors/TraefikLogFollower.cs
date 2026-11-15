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

/// <summary>Follows Traefik's JSON access log and credits bytes that crossed the VPS to the user streaming from that IP.</summary>
public sealed class TraefikLogFollower(
    ILogSource source, IDbContextFactory<StatusDb> dbFactory, IOptions<StatusOptions> options, TimeProvider clock, ILogger<TraefikLogFollower> log)
    : BackgroundService
{
    private readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
    private int _unparsable;

    public int Unparsable => _unparsable;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                return;
            }
            catch (Exception e)
            {
                log.LogWarning(e, "traefik log stream failed, reconnecting in {Backoff}", backoff);
            }
            await Task.Delay(backoff, clock, stoppingToken);
            backoff = TimeSpan.FromSeconds(Math.Min(60, backoff.TotalSeconds * 2));
        }
    }

    /// <summary>Consumes the source until it ends. Small gaps at pod rotation are accepted; the number is labelled approximate.</summary>
    public async Task FollowOnceAsync(CancellationToken ct)
    {
        await foreach (var raw in source.ReadLinesAsync(ct))
        {
            var line = TraefikAccessLine.TryParse(raw);
            if (line is null)
            {
                Interlocked.Increment(ref _unparsable);
                continue;
            }
            await CreditAsync(line, ct);
        }
    }

    private async Task CreditAsync(TraefikAccessLine line, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ip = Attribution.StripPort(line.ClientAddr);
        var leases = await db.Leases.Where(l => l.RemoteIp == ip).ToListAsync(ct);
        var credit = Attribution.Attribute(line, leases, now);
        if (credit is null || credit.Bytes <= 0) return;
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, _tz).DateTime);
        await db.AddVpsBytesAsync(day, credit.UserId, credit.DisplayName, credit.Bytes, ct);
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

        var channel = System.Threading.Channels.Channel.CreateBounded<string>(1024);
        var readers = names.Select(name => PumpAsync(ns, name, channel.Writer, ct)).ToList();
        _ = Task.WhenAll(readers).ContinueWith(_ => channel.Writer.TryComplete(), CancellationToken.None);

        await foreach (var line in channel.Reader.ReadAllAsync(ct)) yield return line;
    }

    private async Task PumpAsync(string ns, string pod, System.Threading.Channels.ChannelWriter<string> writer, CancellationToken ct)
    {
        using var stream = await k8s.CoreV1.ReadNamespacedPodLogAsync(ns, pod, container: "traefik", follow: true, sinceSeconds: 60, cancellationToken: ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is { } line) await writer.WriteAsync(line, ct);
    }
}

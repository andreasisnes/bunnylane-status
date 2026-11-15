namespace BunnyLane.Status;

/// <summary>Bound from the <c>Status</c> configuration section (<c>STATUS__*</c> environment variables).</summary>
public sealed class StatusOptions
{
    public const string Section = "Status";

    public string PrometheusUrl { get; set; } = "http://kps-prometheus.monitoring.svc:9090";
    public TimeSpan PrometheusTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan PrometheusCache { get; set; } = TimeSpan.FromSeconds(10);
    public string JellyfinUrl { get; set; } = "http://jellyfin.jellyfin.svc:8096";
    public string JellyfinApiKey { get; set; } = string.Empty;
    /// <summary>FlokiNet bills total traffic, both directions summed. 6 TB decimal.</summary>
    public long VpsCapBytes { get; set; } = 6_000_000_000_000;
    public string TimeZone { get; set; } = "Europe/Oslo";
    public string[] HiddenUsers { get; set; } = [];
    public string TraefikNamespace { get; set; } = "traefik";
    public string SqlitePath { get; set; } = "status.db";
}

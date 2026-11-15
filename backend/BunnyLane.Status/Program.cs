using BunnyLane.Status;
using BunnyLane.Status.Api;
using BunnyLane.Status.Collectors;
using BunnyLane.Status.Commands;
using BunnyLane.Status.Commands.Handlers;
using BunnyLane.Status.Data;
using BunnyLane.Status.Jellyfin;
using BunnyLane.Status.Metrics;
using k8s;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, WebRootPath = "wwwroot" });
var services = builder.Services;

services.Configure<StatusOptions>(builder.Configuration.GetSection(StatusOptions.Section));
services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
services.AddSingleton(TimeProvider.System);
services.AddMemoryCache();
services.AddSingleton<PrometheusHealth>();
services.AddHttpClient<IPrometheus, PrometheusClient>((sp, http) =>
    http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<StatusOptions>>().Value.PrometheusUrl));
services.AddHttpClient<IJellyfin, JellyfinClient>((sp, http) =>
    http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<StatusOptions>>().Value.JellyfinUrl));
services.AddDbContextFactory<StatusDb>((sp, o) =>
    o.UseSqlite($"Data Source={sp.GetRequiredService<IOptions<StatusOptions>>().Value.SqlitePath}"));

services.AddSingleton<ICommandHandler, HelpCommand>();
services.AddSingleton<ICommandHandler, StatusCommand>();
services.AddSingleton<ICommandHandler, NodesCommand>();
services.AddSingleton<ICommandHandler, NetCommand>();
services.AddSingleton<ICommandHandler, DisksCommand>();
services.AddSingleton<ICommandHandler, NasCommand>();
services.AddSingleton<ICommandHandler, GpuCommand>();
services.AddSingleton<ICommandHandler, NowCommand>();
services.AddSingleton<ICommandHandler, VpsCommand>();
services.AddSingleton<ICommandHandler, TopCommand>();
services.AddSingleton<CommandRegistry>();
services.AddSingleton<CommandRunner>();

services.AddSingleton<ILogSource>(sp =>
{
    var log = sp.GetRequiredService<ILogger<KubernetesLogSource>>();
    try
    {
        var config = KubernetesClientConfiguration.IsInCluster()
            ? KubernetesClientConfiguration.InClusterConfig()
            : KubernetesClientConfiguration.BuildDefaultConfig();
        return new KubernetesLogSource(new Kubernetes(config), sp.GetRequiredService<IOptions<StatusOptions>>(), log);
    }
    catch (Exception e)
    {
        log.LogWarning(e, "no kubernetes access: vps attribution disabled");
        return new NoLogSource();
    }
});
services.AddHostedService<SessionSampler>();
services.AddHostedService<TraefikLogFollower>();
services.AddStatusRateLimiting();
services.AddStatusForwardedHeaders();
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 4 * 1024);

var app = builder.Build();

await using (var db = await app.Services.GetRequiredService<IDbContextFactory<StatusDb>>().CreateDbContextAsync())
{
    await db.Database.EnsureCreatedAsync();
}

app.UseTrustedForwardedHeaders();
app.UseSecurityHeaders();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () => Results.Text("ok"));
app.MapGet("/readyz", (PrometheusHealth health) =>
    health.IsReady() ? Results.Text("ready") : Results.Text("prometheus has not answered recently", statusCode: StatusCodes.Status503ServiceUnavailable));
app.MapCommandEndpoint();
app.MapStreamEndpoints();

app.Run();

public partial class Program;

/// <summary>Used when the process has no Kubernetes credentials; the follower logs and retries with backoff.</summary>
file sealed class NoLogSource : ILogSource
{
    public async IAsyncEnumerable<string> ReadLinesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), ct);
        yield break;
    }
}

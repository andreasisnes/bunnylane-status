using BunnyLane.Status.Collectors;
using BunnyLane.Status.Data;
using BunnyLane.Status.Jellyfin;
using BunnyLane.Status.Metrics;
using BunnyLane.Status.Tests.Collectors;
using BunnyLane.Status.Tests.Commands.Handlers;
using BunnyLane.Status.Tests.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BunnyLane.Status.Tests.Api;

/// <summary>The real app with Prometheus, Jellyfin, the database and the log source replaced by fakes.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public FakePrometheus Prom { get; } = FakePrometheus.Cluster();
    public FakeJellyfin Jellyfin { get; } = new(new Session("u1", "anna", "84.212.10.5", true, 8_000_000, true));
    public TestDb Db { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Status:SqlitePath", ":memory:");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPrometheus>();
            services.AddSingleton<IPrometheus>(Prom);
            services.RemoveAll<IJellyfin>();
            services.AddSingleton<IJellyfin>(Jellyfin);
            services.RemoveAll<IDbContextFactory<StatusDb>>();
            services.AddSingleton<IDbContextFactory<StatusDb>>(Db);
            services.RemoveAll<ILogSource>();
            services.AddSingleton<ILogSource>(new FakeLogSource());
        });
    }

    /// <summary>Reads SSE events ("event:" + "data:" pairs) from a response stream until <paramref name="count"/> arrive.</summary>
    public static Task<List<(string Event, string Data)>> ReadEventsAsync(Stream stream, int count, CancellationToken ct) =>
        ReadEventsAsync(new StreamReader(stream, leaveOpen: true), count, ct);

    /// <summary>Use one reader for repeated reads from the same stream; the reader buffers ahead.</summary>
    public static async Task<List<(string Event, string Data)>> ReadEventsAsync(StreamReader reader, int count, CancellationToken ct)
    {
        var events = new List<(string, string)>();
        string? type = null;
        var data = new System.Text.StringBuilder();
        while (events.Count < count && await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.StartsWith("event:")) type = line[6..].Trim();
            else if (line.StartsWith("data:")) data.Append(line[5..].Trim());
            else if (line.Length == 0 && data.Length > 0)
            {
                events.Add((type ?? "message", data.ToString()));
                type = null;
                data.Clear();
            }
        }
        return events;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) Db.Dispose();
    }
}

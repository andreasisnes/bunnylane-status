using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace BunnyLane.Status.Api;

public static class RateLimit
{
    public const string Commands = "commands";
    public const string Streams = "streams";
    public const string Ticker = "ticker";
    public const int CommandsPerSecond = 10;
    public const int StreamsPerClient = 4;
    /// <summary>Every open tab holds one; generous so a household behind one IP is fine.</summary>
    public const int TickersPerClient = 16;

    public static IServiceCollection AddStatusRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(Commands, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = CommandsPerSecond,
                Window = TimeSpan.FromSeconds(1),
                QueueLimit = 0,
            }));
            o.AddPolicy(Streams, ctx => RateLimitPartition.GetConcurrencyLimiter(ClientKey(ctx), _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = StreamsPerClient,
                QueueLimit = 0,
            }));
            o.AddPolicy(Ticker, ctx => RateLimitPartition.GetConcurrencyLimiter("t|" + ClientKey(ctx), _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = TickersPerClient,
                QueueLimit = 0,
            }));
        });

    /// <summary>The client IP after forwarded-header processing; unknown clients share one bucket.</summary>
    public static string ClientKey(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

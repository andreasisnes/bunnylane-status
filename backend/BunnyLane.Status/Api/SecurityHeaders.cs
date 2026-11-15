using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace BunnyLane.Status.Api;

public static class SecurityHeaders
{
    /// <summary>Self plus the two Google Fonts origins. Inline styles are needed for React style attributes and uPlot.</summary>
    public const string Csp =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "base-uri 'self'; form-action 'none'; frame-ancestors 'none'";

    /// <summary>The cluster pod network: only Traefik's forwarded headers are believed.</summary>
    public static readonly System.Net.IPNetwork TrustedProxies = System.Net.IPNetwork.Parse("10.244.0.0/16");

    public static IServiceCollection AddStatusForwardedHeaders(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
            o.KnownIPNetworks.Add(TrustedProxies);
            o.ForwardLimit = 1;
        });

    /// <summary>
    /// Drops X-Forwarded-* unless the TCP peer is a trusted proxy. The built-in middleware skips the check when the
    /// remote address is unknown, so this runs first and makes the trust decision explicit.
    /// </summary>
    public static IApplicationBuilder UseTrustedForwardedHeaders(this IApplicationBuilder app)
    {
        app.Use(async (ctx, next) =>
        {
            var peer = ctx.Connection.RemoteIpAddress;
            if (peer is null || !TrustedProxies.Contains(peer.IsIPv4MappedToIPv6 ? peer.MapToIPv4() : peer))
            {
                ctx.Request.Headers.Remove("X-Forwarded-For");
                ctx.Request.Headers.Remove("X-Forwarded-Proto");
                ctx.Request.Headers.Remove("X-Forwarded-Host");
            }
            await next();
        });
        return app.UseForwardedHeaders();
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var h = ctx.Response.Headers;
            h.ContentSecurityPolicy = Csp;
            h.XContentTypeOptions = "nosniff";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            h.XFrameOptions = "DENY";
            await next();
        });
}

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using BunnyLane.Status.Data;

namespace BunnyLane.Status.Collectors;

public sealed record TraefikAccessLine(string ClientAddr, string RequestHost, long DownstreamContentSize, DateTimeOffset Time)
{
    /// <summary>Parses one JSON access-log line; returns null for anything that is not an access entry.</summary>
    public static TraefikAccessLine? TryParse(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("ClientAddr", out var addr) || !root.TryGetProperty("RequestHost", out var host)) return null;
            var size = root.TryGetProperty("DownstreamContentSize", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0;
            var time = root.TryGetProperty("time", out var t) && t.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(t.GetString(), out var parsed)
                ? parsed : DateTimeOffset.MinValue;
            return new TraefikAccessLine(addr.GetString() ?? "", host.GetString() ?? "", size, time);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record Credit(string UserId, string DisplayName, long Bytes);

/// <summary>Pure rules for turning an access-log line into a leaderboard credit.</summary>
public static class Attribution
{
    public const string Other = "other";
    public const string JellyfinHost = "jellyfin.bunnylane.no";
    public static readonly TimeSpan LeaseWindow = TimeSpan.FromMinutes(10);
    private static readonly IPNetwork Tunnel = IPNetwork.Parse("10.200.0.0/24");

    public static Credit? Attribute(TraefikAccessLine line, IReadOnlyList<SessionLease> leases, DateTimeOffset now)
    {
        if (!string.Equals(line.RequestHost, JellyfinHost, StringComparison.OrdinalIgnoreCase)) return null;
        var ip = StripPort(line.ClientAddr);
        if (!IsPublic(ip)) return null;

        var lease = leases.FirstOrDefault(l => l.RemoteIp == ip && now - l.SeenUtc <= LeaseWindow);
        return lease is null
            ? new Credit(Other, "", line.DownstreamContentSize)
            : new Credit(lease.UserId, lease.DisplayName, line.DownstreamContentSize);
    }

    /// <summary>Public means it crossed the VPS: not RFC 1918, not loopback, not link-local, not the WireGuard tunnel, not ULA.</summary>
    public static bool IsPublic(string ip)
    {
        if (!IPAddress.TryParse(ip, out var address)) return false;
        if (IPAddress.IsLoopback(address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal) return false;
            if (address.IsIPv4MappedToIPv6) return IsPublic(address.MapToIPv4().ToString());
            return true;
        }
        var b = address.GetAddressBytes();
        if (b[0] == 10 || b[0] == 127 || (b[0] == 169 && b[1] == 254)) return false;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;
        if (b[0] == 192 && b[1] == 168) return false;
        if (Tunnel.Contains(address)) return false;
        return true;
    }

    public static string StripPort(string endpoint) =>
        IPEndPoint.TryParse(endpoint, out var ep) ? ep.Address.ToString() : endpoint;
}

using BunnyLane.Status.Collectors;
using BunnyLane.Status.Data;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Collectors;

public class AttributionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-15T20:00:00Z");
    private static readonly SessionLease Anna = new() { RemoteIp = "84.212.10.5", UserId = "u1", DisplayName = "anna", SeenUtc = Now.AddMinutes(-2) };

    private static TraefikAccessLine Line(string ip, string host = "jellyfin.bunnylane.no", long bytes = 1_000_000) =>
        new(ip, host, bytes, Now);

    [Fact]
    public void Public_ip_with_a_fresh_lease_credits_the_user() =>
        Attribution.Attribute(Line("84.212.10.5"), [Anna], Now).Should().Be(new Credit("u1", "anna", 1_000_000));

    [Theory]
    [InlineData("192.168.30.5")]
    [InlineData("10.200.0.1")]
    [InlineData("10.244.1.7")]
    [InlineData("172.16.0.9")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    public void Private_tunnel_and_loopback_addresses_credit_nothing(string ip) =>
        Attribution.Attribute(Line(ip), [Anna], Now).Should().BeNull();

    [Fact]
    public void Public_ip_without_a_lease_credits_other() =>
        Attribution.Attribute(Line("8.8.8.8"), [Anna], Now).Should().Be(new Credit("other", "", 1_000_000));

    [Fact]
    public void A_stale_lease_credits_other()
    {
        var stale = Anna with { SeenUtc = Now.AddMinutes(-11) };

        Attribution.Attribute(Line("84.212.10.5"), [stale], Now).Should().Be(new Credit("other", "", 1_000_000));
    }

    [Fact]
    public void Other_hosts_are_ignored() =>
        Attribution.Attribute(Line("84.212.10.5", host: "seerr.bunnylane.no"), [Anna], Now).Should().BeNull();

    [Fact]
    public void Client_address_may_carry_a_port() =>
        Attribution.Attribute(Line("84.212.10.5:40212"), [Anna], Now)!.UserId.Should().Be("u1");

    [Fact]
    public void Parses_a_traefik_json_access_line()
    {
        const string json = """
            {"ClientAddr":"84.212.10.5:40212","ClientHost":"84.212.10.5","DownstreamContentSize":734003,"RequestHost":"jellyfin.bunnylane.no","RequestPath":"/Videos/abc/stream","time":"2026-11-15T20:00:01+01:00","level":"info"}
            """;

        var line = TraefikAccessLine.TryParse(json);

        line.Should().Be(new TraefikAccessLine("84.212.10.5:40212", "jellyfin.bunnylane.no", 734003, DateTimeOffset.Parse("2026-11-15T20:00:01+01:00")));
        TraefikAccessLine.TryParse("not json").Should().BeNull();
        TraefikAccessLine.TryParse("{\"level\":\"info\",\"msg\":\"Configuration loaded\"}").Should().BeNull();
    }
}

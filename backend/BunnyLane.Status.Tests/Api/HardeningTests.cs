using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Api;

public sealed class HardeningTests
{
    [Fact]
    public async Task Responses_carry_a_content_security_policy_allowing_self_and_the_font_origins()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().GetAsync("/healthz");

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("https://fonts.googleapis.com").And.Contain("https://fonts.gstatic.com");
        csp.Should().Contain("frame-ancestors 'none'");
        response.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
        response.Headers.GetValues("Referrer-Policy").Single().Should().Be("no-referrer");
        response.Headers.Contains("Strict-Transport-Security").Should().BeFalse("TLS terminates at Traefik");
    }

    [Fact]
    public async Task Forwarded_for_from_an_untrusted_source_does_not_split_the_rate_limit_bucket()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 15; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/command") { Content = JsonContent.Create(new { line = "help" }) };
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{i}");
            statuses.Add((await client.SendAsync(request)).StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests, "spoofed client addresses must not each get their own bucket");
    }

    [Fact]
    public async Task Static_index_is_served_with_the_same_headers()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().GetAsync("/readyz");

        response.Headers.Contains("Content-Security-Policy").Should().BeTrue();
    }
}

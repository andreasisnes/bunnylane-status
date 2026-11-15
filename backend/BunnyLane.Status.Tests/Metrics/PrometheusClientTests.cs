using System.Net;
using BunnyLane.Status.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace BunnyLane.Status.Tests.Metrics;

public sealed class PrometheusClientTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    private PrometheusClient Client(TimeSpan? timeout = null)
    {
        var options = new StatusOptions { PrometheusUrl = _server.Url!, PrometheusTimeout = timeout ?? TimeSpan.FromSeconds(5) };
        var http = new HttpClient { BaseAddress = new Uri(options.PrometheusUrl) };
        return new PrometheusClient(http, new MemoryCache(new MemoryCacheOptions()), Options.Create(options), new PrometheusHealth(TimeProvider.System));
    }

    private const string InstantBody = """
        {"status":"success","data":{"resultType":"vector","result":[
          {"metric":{"__name__":"up","job":"vps","instance":"10.200.0.1:9100"},"value":[1700000000.5,"1"]},
          {"metric":{"job":"kubelet"},"value":[1700000000.5,"0.25"]}]}}
        """;

    [Fact]
    public async Task Instant_query_parses_labels_and_values()
    {
        _server.Given(Request.Create().WithPath("/api/v1/query").WithParam("query", "up").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(InstantBody));

        var result = await Client().QueryAsync("up", CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Labels["job"].Should().Be("vps");
        result[0].Value.Should().Be(1);
        result[1].Value.Should().Be(0.25);
    }

    [Fact]
    public async Task Server_error_raises_MetricsUnavailableException()
    {
        _server.Given(Request.Create().WithPath("/api/v1/query").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        await Client().Invoking(c => c.QueryAsync("up", CancellationToken.None))
            .Should().ThrowAsync<MetricsUnavailableException>();
    }

    [Fact]
    public async Task Slow_response_raises_MetricsUnavailableException()
    {
        _server.Given(Request.Create().WithPath("/api/v1/query").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(InstantBody).WithDelay(TimeSpan.FromSeconds(2)));

        await Client(TimeSpan.FromMilliseconds(200)).Invoking(c => c.QueryAsync("up", CancellationToken.None))
            .Should().ThrowAsync<MetricsUnavailableException>();
    }

    [Fact]
    public void Default_timeout_is_five_seconds() =>
        new StatusOptions().PrometheusTimeout.Should().Be(TimeSpan.FromSeconds(5));

    [Fact]
    public async Task Identical_queries_within_the_cache_window_hit_the_server_once()
    {
        _server.Given(Request.Create().WithPath("/api/v1/query").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(InstantBody));
        var client = Client();

        await client.QueryAsync("up", CancellationToken.None);
        await client.QueryAsync("up", CancellationToken.None);
        await client.QueryAsync("up{job=\"vps\"}", CancellationToken.None);

        _server.LogEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Range_query_fills_missing_steps_with_null()
    {
        _server.Given(Request.Create().WithPath("/api/v1/query_range").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""
                {"status":"success","data":{"resultType":"matrix","result":[
                  {"metric":{"device":"eth0"},"values":[[1700000000,"10"],[1700000120,"30"]]}]}}
                """));
        var from = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        var to = DateTimeOffset.FromUnixTimeSeconds(1700000120);

        var series = await Client().QueryRangeAsync("x", from, to, TimeSpan.FromSeconds(60), CancellationToken.None);

        series.Should().ContainSingle();
        series[0].Labels["device"].Should().Be("eth0");
        series[0].Timestamps.Should().Equal(1700000000, 1700000060, 1700000120);
        series[0].Values.Should().Equal(10, null, 30);
        _server.LogEntries.Single().RequestMessage.Query!["step"].Should().Contain("60");
    }

    [Fact]
    public void Options_bind_defaults_from_the_spec()
    {
        var o = new StatusOptions();

        o.VpsCapBytes.Should().Be(6_000_000_000_000);
        o.TimeZone.Should().Be("Europe/Oslo");
        o.TraefikNamespace.Should().Be("traefik");
        o.HiddenUsers.Should().BeEmpty();
    }

    public void Dispose() => _server.Stop();
}

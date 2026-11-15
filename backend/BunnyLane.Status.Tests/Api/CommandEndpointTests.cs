using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BunnyLane.Status.Render;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Api;

public sealed class CommandEndpointTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ApiFactory _factory;

    public CommandEndpointTests(ApiFactory factory) => _factory = factory;

    private async Task<CommandResult> Post(HttpClient client, string line)
    {
        var response = await client.PostAsJsonAsync("/api/command", new { line });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CommandResult>(Json))!;
    }

    [Fact]
    public async Task Help_returns_a_table()
    {
        var result = await Post(_factory.CreateClient(), "help");

        result.Status.Should().Be(ResultStatus.Ok);
        result.Blocks.Should().ContainSingle().Which.Should().BeOfType<TableBlock>();
    }

    [Fact]
    public async Task Status_runs_against_the_fake_cluster()
    {
        var result = await Post(_factory.CreateClient(), "status");

        result.Blocks.OfType<TagsBlock>().Single().Tags.Should().ContainEquivalentOf(new Tag("nodes 6/6", Level.Ok));
    }

    [Fact]
    public async Task Shell_like_input_is_not_found_and_echoed_back()
    {
        var result = await Post(_factory.CreateClient(), "net; id");

        result.Status.Should().Be(ResultStatus.Error);
        var text = result.Blocks.Should().ContainSingle().Which.Should().BeOfType<TextBlock>().Subject.Text;
        text.Should().Contain("command not found").And.Contain("net; id").And.Contain("try help");
    }

    [Fact]
    public async Task Oversized_body_is_rejected_with_413()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/command", new { line = new string('a', 300) });

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Malformed_body_is_a_400_not_a_500()
    {
        var response = await _factory.CreateClient().PostAsync("/api/command", new StringContent("{nope", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Readyz_follows_prometheus_health()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        (await client.GetAsync("/readyz")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var health = (BunnyLane.Status.Metrics.PrometheusHealth)factory.Services.GetService(typeof(BunnyLane.Status.Metrics.PrometheusHealth))!;
        health.MarkSuccess();

        (await client.GetAsync("/readyz")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Command_burst_from_one_client_is_rate_limited()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 15; i++) statuses.Add((await client.PostAsJsonAsync("/api/command", new { line = "help" })).StatusCode);

        statuses.Should().Contain(HttpStatusCode.TooManyRequests);
        statuses.Take(10).Should().OnlyContain(s => s == HttpStatusCode.OK);
    }
}

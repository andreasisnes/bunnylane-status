using System.Text.Json;
using System.Text.RegularExpressions;
using BunnyLane.Status.Commands;
using BunnyLane.Status.Metrics;
using BunnyLane.Status.Commands.Handlers;
using BunnyLane.Status.Render;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Commands.Handlers;

public class ReadOnlyCommandTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Regex Forbidden = new(@"k8s-0|cp01|w01|192\.168|servarr|jellyfin|paabrygga|9100", RegexOptions.IgnoreCase);

    private static ICommandHandler[] All(FakePrometheus p) =>
    [
        new StatusCommand(p), new NodesCommand(p), new NetCommand(p), new DisksCommand(p), new NasCommand(p), new GpuCommand(p),
    ];

    [Fact]
    public async Task Status_reports_nodes_and_degraded_workloads()
    {
        var result = await new StatusCommand(FakePrometheus.Cluster()).RunAsync([], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
        var tags = result.Blocks.OfType<TagsBlock>().Single().Tags;
        tags.Should().ContainEquivalentOf(new Tag("nodes 6/6", Level.Ok));
        tags.Should().ContainEquivalentOf(new Tag("2 workloads", Level.Degraded));
        tags.Should().ContainEquivalentOf(new Tag("storage", Level.Degraded));
        tags.Should().ContainEquivalentOf(new Tag("edge", Level.Ok));
    }

    [Fact]
    public async Task Status_degrades_to_unknown_when_prometheus_is_down()
    {
        var result = await new StatusCommand(new FakePrometheus { Down = true }).RunAsync([], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Unknown);
        result.Blocks.OfType<TagsBlock>().Single().Tags.Should().OnlyContain(t => t.Level == Level.Unknown);
    }

    [Fact]
    public async Task No_handler_leaks_hostnames_ips_or_workload_names()
    {
        foreach (var handler in All(FakePrometheus.Cluster()))
        {
            var result = await handler.RunAsync([], CancellationToken.None);
            var json = JsonSerializer.Serialize(result, Json);

            Forbidden.IsMatch(json).Should().BeFalse($"{handler.Name} leaked: {json}");
        }
    }

    [Fact]
    public async Task Every_handler_returns_unknown_not_an_exception_when_prometheus_is_down()
    {
        foreach (var handler in All(new FakePrometheus { Down = true }))
        {
            var result = await handler.RunAsync([], CancellationToken.None);
            result.Status.Should().Be(ResultStatus.Unknown, handler.Name);
        }
    }

    [Fact]
    public async Task Nodes_are_labelled_by_role_and_sorted()
    {
        var result = await new NodesCommand(FakePrometheus.Cluster()).RunAsync([], CancellationToken.None);

        var table = result.Blocks.OfType<TableBlock>().Single();
        table.Rows.Select(r => r[0]).Should().Equal("cp-1", "cp-2", "cp-3", "w-1", "w-2", "w-3");
        table.Rows[0].Should().Contain("3d 4h");
    }

    [Theory]
    [InlineData("1h", 30)]
    [InlineData("24h", 300)]
    [InlineData("7d", 1800)]
    [InlineData("30d", 7200)]
    public async Task Net_requests_a_range_with_the_step_for_the_window(string window, int stepSeconds)
    {
        var p = FakePrometheus.Cluster();

        var result = await new NetCommand(p).RunAsync([window], CancellationToken.None);

        p.RangeCalls.Should().OnlyContain(c => c.Step == TimeSpan.FromSeconds(stepSeconds));
        var chart = result.Blocks.OfType<ChartBlock>().Single();
        chart.Series.Select(s => s.Name).Should().Equal("in", "out");
        result.Blocks.OfType<TextBlock>().First().Text.Should().Contain("184 Mbit/s");
    }

    [Fact]
    public async Task Net_defaults_to_one_hour() =>
        (await new NetCommand(FakePrometheus.Cluster()).RunAsync([], CancellationToken.None))
            .Blocks.OfType<ChartBlock>().Single().Title.Should().Be("net 1h");

    [Fact]
    public async Task Disks_shows_a_bar_per_worker_and_flags_degraded_volumes()
    {
        var result = await new DisksCommand(FakePrometheus.Cluster()).RunAsync([], CancellationToken.None);

        result.Blocks.OfType<BarBlock>().Should().HaveCount(3);
        result.Blocks.OfType<BarBlock>().First().Fraction.Should().BeApproximately(200.0 / 512, 0.001);
        result.Blocks.OfType<TagsBlock>().Single().Tags.Should().ContainEquivalentOf(new Tag("1 volume degraded", Level.Degraded));
    }

    [Fact]
    public async Task Nas_shows_used_total_and_free()
    {
        var result = await new NasCommand(FakePrometheus.Cluster()).RunAsync([], CancellationToken.None);

        var bar = result.Blocks.OfType<BarBlock>().Single();
        bar.Fraction.Should().BeApproximately(6.8 / 37, 0.001);
        bar.Caption.Should().Contain("6.80 TB").And.Contain("37.0 TB");
    }

    [Fact]
    public async Task Gpu_without_drm_metrics_is_unknown()
    {
        var p = FakePrometheus.Cluster().Set(Promql.GpuBusy);

        var result = await new GpuCommand(p).RunAsync([], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Unknown);
    }

    [Fact]
    public async Task Gpu_lists_each_worker_with_busy_vram_and_temperature()
    {
        var result = await new GpuCommand(FakePrometheus.Cluster()).RunAsync(["24h"], CancellationToken.None);

        var table = result.Blocks.OfType<TableBlock>().Single();
        table.Rows.Should().HaveCount(3);
        table.Rows[0].Should().Contain("42%").And.Contain("61 °C");
        result.Blocks.OfType<ChartBlock>().Single().Title.Should().Be("gpu 24h");
    }
}

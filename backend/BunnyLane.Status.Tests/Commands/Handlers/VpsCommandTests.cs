using BunnyLane.Status.Commands.Handlers;
using BunnyLane.Status.Metrics;
using BunnyLane.Status.Render;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Tests.Commands.Handlers;

public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public class VpsCommandTests
{
    // 2026-11-16 00:00 Oslo: 15 of 30 days elapsed.
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-15T23:00:00Z");
    private static readonly DateTimeOffset MonthStart = DateTimeOffset.Parse("2026-10-31T23:00:00Z");

    private static FakePrometheus Prom(double rxTotal, double txTotal)
    {
        var p = FakePrometheus.Cluster();
        // Hourly grid from month start to now, linear growth; 15 days × 24 = 360 steps.
        var steps = (int)((Now - MonthStart).TotalHours) + 1;
        var grid = Enumerable.Range(0, steps).Select(i => MonthStart.ToUnixTimeSeconds() + i * 3600L).ToArray();
        double?[] Linear(double total) => Enumerable.Range(0, steps).Select(i => (double?)(1e9 + total * i / (steps - 1))).ToArray();
        p.SetRange(Promql.VpsRx, new RangeSeries(new Dictionary<string, string>(), grid, Linear(rxTotal)));
        p.SetRange(Promql.VpsTx, new RangeSeries(new Dictionary<string, string>(), grid, Linear(txTotal)));
        return p;
    }

    private static VpsCommand Cmd(FakePrometheus p, long cap = 6_000_000_000_000) =>
        new(p, Options.Create(new StatusOptions { VpsCapBytes = cap }), new FixedClock(Now));

    [Fact]
    public async Task Vps_reports_used_against_cap_with_projection()
    {
        var p = Prom(2.0e12, 0.31e12);

        var result = await Cmd(p).RunAsync([], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
        var bar = result.Blocks.OfType<BarBlock>().Single();
        bar.Fraction.Should().BeApproximately(2.31 / 6, 0.001);
        bar.Caption.Should().Contain("2.31 TB").And.Contain("6.00 TB");
        var text = string.Join("\n", result.Blocks.OfType<TextBlock>().Select(t => t.Text));
        text.Should().Contain("in 2.00 TB").And.Contain("out 310 GB").And.Contain("projected 4.62 TB").And.Contain("15 days to reset");
        result.Blocks.OfType<SparklineBlock>().Single().Values.Should().HaveCount(15);
        p.RangeCalls.Should().OnlyContain(c => c.Step == TimeSpan.FromHours(1) && c.From == MonthStart);
    }

    [Fact]
    public async Task Vps_fraction_is_capped_at_one()
    {
        var result = await Cmd(Prom(5e12, 3e12)).RunAsync([], CancellationToken.None);

        result.Blocks.OfType<BarBlock>().Single().Fraction.Should().Be(1.0);
    }

    [Fact]
    public async Task Vps_month_lists_the_last_six_months()
    {
        var result = await Cmd(Prom(1e12, 1e12)).RunAsync(["month"], CancellationToken.None);

        var table = result.Blocks.OfType<TableBlock>().Single();
        table.Columns.Should().Equal("month", "in", "out", "total");
        table.Rows.Should().HaveCount(6);
        table.Rows[^1][0].Should().Be("2026-11");
        table.Rows[0][0].Should().Be("2026-06");
    }

    [Fact]
    public async Task Vps_without_counter_samples_is_unknown_not_zero()
    {
        var p = FakePrometheus.Cluster();
        p.SetRange(Promql.VpsRx);
        p.SetRange(Promql.VpsTx);

        (await Cmd(p).RunAsync([], CancellationToken.None)).Status.Should().Be(ResultStatus.Unknown);
    }

    [Fact]
    public async Task Vps_is_unknown_when_prometheus_is_down() =>
        (await Cmd(new FakePrometheus { Down = true }).RunAsync([], CancellationToken.None)).Status.Should().Be(ResultStatus.Unknown);

    [Fact]
    public async Task Vps_colours_the_bar_against_the_cap()
    {
        var bar = (await Cmd(Prom(2.0e12, 0.31e12)).RunAsync([], CancellationToken.None)).Blocks.OfType<BarBlock>().Single();

        bar.Level.Should().Be(Level.Ok);     // 2.31 of 6 TB is 39%
    }

    [Fact]
    public async Task Vps_over_eighty_percent_of_the_cap_is_down()
    {
        var bar = (await Cmd(Prom(5.0e12, 0.2e12)).RunAsync([], CancellationToken.None)).Blocks.OfType<BarBlock>().Single();

        bar.Level.Should().Be(Level.Down);   // 5.2 of 6 TB is 87%
    }
}

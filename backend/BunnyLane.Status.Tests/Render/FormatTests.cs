using BunnyLane.Status.Render;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Render;

public class FormatTests
{
    [Theory]
    [InlineData(2_310_000_000_000, "2.31 TB")]
    [InlineData(41_000_000_000, "41.0 GB")]
    [InlineData(512, "512 B")]
    [InlineData(0, "0 B")]
    public void Bytes_uses_decimal_units_with_three_significant_digits(double bytes, string expected) =>
        Format.Bytes(bytes).Should().Be(expected);

    [Theory]
    [InlineData(184_000_000, "184 Mbit/s")]
    [InlineData(1_500_000_000, "1.50 Gbit/s")]
    [InlineData(950, "950 bit/s")]
    public void Bits_formats_bits_per_second(double bps, string expected) =>
        Format.Bits(bps).Should().Be(expected);

    [Theory]
    [InlineData(76, "3d 4h")]
    [InlineData(5, "5h 0m")]
    public void Duration_formats_days_and_hours(int hours, string expected) =>
        Format.Duration(TimeSpan.FromHours(hours)).Should().Be(expected);

    [Fact]
    public void Duration_under_an_hour_shows_minutes() =>
        Format.Duration(TimeSpan.FromMinutes(42)).Should().Be("42m");

    [Theory]
    [InlineData(0.63, "63%")]
    [InlineData(0.005, "1%")]
    [InlineData(1.0, "100%")]
    public void Percent_rounds_to_whole_percent(double fraction, string expected) =>
        Format.Percent(fraction).Should().Be(expected);

    [Fact]
    public void Bar_fills_rounded_fraction_of_width()
    {
        var bar = Format.Bar(0.385, 24);

        bar.Should().HaveLength(24);
        bar.Count(c => c == '█').Should().Be(9);
        bar.Count(c => c == '░').Should().Be(15);
    }

    [Fact]
    public void Bar_clamps_fraction_to_unit_interval()
    {
        Format.Bar(1.7, 10).Should().Be(new string('█', 10));
        Format.Bar(-1, 10).Should().Be(new string('░', 10));
    }

    [Fact]
    public void Spark_scales_values_to_eight_levels_and_ends_with_full_block()
    {
        var spark = Format.Spark([0, 1, 2, 4, 8]);

        spark.Should().HaveLength(5);
        spark[0].Should().Be('▁');
        spark[^1].Should().Be('█');
    }

    [Fact]
    public void Spark_of_constant_series_is_flat_and_empty_series_is_empty()
    {
        Format.Spark([3, 3, 3]).Should().Be("▁▁▁");
        Format.Spark([]).Should().BeEmpty();
    }
}

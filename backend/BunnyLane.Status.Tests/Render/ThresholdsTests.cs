using BunnyLane.Status.Render;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Render;

public class ThresholdsTests
{
    [Theory]
    [InlineData(0.0, Level.Ok)]
    [InlineData(0.599, Level.Ok)]
    [InlineData(0.6, Level.Degraded)]     // lower bound inclusive
    [InlineData(0.8, Level.Degraded)]     // upper bound inclusive
    [InlineData(0.801, Level.Down)]
    [InlineData(1.0, Level.Down)]
    [InlineData(-0.5, Level.Ok)]          // clamped, matches Format.Percent
    [InlineData(1.5, Level.Down)]         // clamped
    public void LevelFor_maps_a_fraction_to_a_band(double fraction, Level expected) =>
        Thresholds.LevelFor(fraction).Should().Be(expected);

    [Fact]
    public void LevelFor_treats_NaN_as_unknown() =>
        Thresholds.LevelFor(double.NaN).Should().Be(Level.Unknown);
}

using BunnyLane.Status.Render;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Render;

public class ThresholdsTests
{
    [Theory]
    [InlineData(0.0, Level.Ok)]
    [InlineData(0.594, Level.Ok)]         // displays 59%
    [InlineData(0.599, Level.Degraded)]   // displays 60%, so it must not be coloured ok
    [InlineData(0.6, Level.Degraded)]     // lower bound inclusive
    [InlineData(0.8, Level.Degraded)]     // upper bound inclusive
    [InlineData(0.801, Level.Degraded)]   // displays 80%, so it must not be coloured hot
    [InlineData(0.805, Level.Down)]       // displays 81%
    [InlineData(1.0, Level.Down)]
    [InlineData(-0.5, Level.Ok)]          // clamped, matches Format.Percent
    [InlineData(1.5, Level.Down)]         // clamped
    public void LevelFor_maps_a_fraction_to_a_band(double fraction, Level expected) =>
        Thresholds.LevelFor(fraction).Should().Be(expected);

    /// <summary>The band must never contradict the number printed beside it.</summary>
    [Theory]
    [InlineData(0.5996)]
    [InlineData(0.8004)]
    [InlineData(0.801)]
    [InlineData(0.6)]
    [InlineData(0.8)]
    public void LevelFor_agrees_with_the_percentage_Format_prints(double fraction)
    {
        var shown = int.Parse(Format.Percent(fraction).TrimEnd('%'));
        var expected = shown < 60 ? Level.Ok : shown <= 80 ? Level.Degraded : Level.Down;

        Thresholds.LevelFor(fraction).Should().Be(expected, "the cell reads {0}%", shown);
    }

    [Fact]
    public void LevelFor_treats_NaN_as_unknown() =>
        Thresholds.LevelFor(double.NaN).Should().Be(Level.Unknown);
}

namespace BunnyLane.Status.Render;

/// <summary>
/// The one place the utilisation bands live. Applies only to a fraction of a capacity —
/// never to a load average, an uptime, a bitrate or a count.
/// </summary>
public static class Thresholds
{
    /// <summary>
    /// Bands the <em>displayed</em> whole percentage, not the raw fraction, so the colour always
    /// agrees with the number next to it: under 60% is ok, 60-80% inclusive is degraded, 81% and
    /// above is down. Banding the raw fraction would let a cell reading "80%" render red.
    /// </summary>
    public static Level LevelFor(double fraction)
    {
        if (double.IsNaN(fraction)) return Level.Unknown;

        var pct = Format.PercentValue(fraction);
        if (pct < 60) return Level.Ok;
        return pct <= 80 ? Level.Degraded : Level.Down;
    }
}

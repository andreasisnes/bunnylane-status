namespace BunnyLane.Status.Render;

/// <summary>
/// The one place the utilisation bands live. Applies only to a fraction of a capacity —
/// never to a load average, an uptime, a bitrate or a count.
/// </summary>
public static class Thresholds
{
    /// <summary>Lower bounds are inclusive: 0.60 is <see cref="Level.Degraded"/>, 0.80 still is, 0.801 is <see cref="Level.Down"/>.</summary>
    public static Level LevelFor(double fraction)
    {
        if (double.IsNaN(fraction)) return Level.Unknown;

        var f = Math.Clamp(fraction, 0, 1);
        if (f < 0.6) return Level.Ok;
        return f <= 0.8 ? Level.Degraded : Level.Down;
    }
}

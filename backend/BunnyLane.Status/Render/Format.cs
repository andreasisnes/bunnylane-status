using System.Globalization;

namespace BunnyLane.Status.Render;

/// <summary>Pure formatting helpers. Decimal units everywhere; three significant digits.</summary>
public static class Format
{
    private static readonly string[] ByteUnits = ["B", "kB", "MB", "GB", "TB", "PB"];
    private static readonly string[] BitUnits = ["bit/s", "kbit/s", "Mbit/s", "Gbit/s", "Tbit/s"];
    private const string SparkLevels = "▁▂▃▄▅▆▇█";

    public static string Bytes(double bytes) => Scaled(bytes, ByteUnits);

    public static string Bits(double bitsPerSecond) => Scaled(bitsPerSecond, BitUnits);

    public static string Duration(TimeSpan span)
    {
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{(int)span.TotalMinutes}m";
    }

    public static string Percent(double fraction) =>
        $"{Math.Round(Math.Clamp(fraction, 0, 1) * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%";

    public static string Bar(double fraction, int width = 24)
    {
        var filled = (int)Math.Round(Math.Clamp(fraction, 0, 1) * width, MidpointRounding.AwayFromZero);
        return new string('█', filled) + new string('░', width - filled);
    }

    public static string Spark(double[] values)
    {
        if (values.Length == 0) return string.Empty;
        var min = values.Min();
        var max = values.Max();
        var range = max - min;
        return string.Create(values.Length, values, (span, v) =>
        {
            for (var i = 0; i < v.Length; i++)
            {
                var level = range <= 0 ? 0 : (int)Math.Round((v[i] - min) / range * (SparkLevels.Length - 1));
                span[i] = SparkLevels[level];
            }
        });
    }

    private static string Scaled(double value, string[] units)
    {
        var v = Math.Max(0, value);
        var unit = 0;
        while (v >= 1000 && unit < units.Length - 1)
        {
            v /= 1000;
            unit++;
        }
        var text = unit == 0 ? v.ToString("0", CultureInfo.InvariantCulture) : ThreeSignificant(v);
        return $"{text} {units[unit]}";
    }

    private static string ThreeSignificant(double v)
    {
        var decimals = v >= 100 ? 0 : v >= 10 ? 1 : 2;
        return v.ToString($"F{decimals}", CultureInfo.InvariantCulture);
    }
}

namespace BunnyLane.Status.Metrics;

public readonly record struct Month(DateTimeOffset Start, DateTimeOffset End)
{
    public string Label => Start.ToString("yyyy-MM");
}

/// <summary>Calendar months in a time zone. The VPS cap resets on the 1st, local time.</summary>
public static class Months
{
    public static Month Current(DateTimeOffset nowUtc, TimeZoneInfo tz) => At(nowUtc, tz, 0);

    /// <summary>The last <paramref name="n"/> months, oldest first, ending with the current one.</summary>
    public static IEnumerable<Month> Last(int n, DateTimeOffset nowUtc, TimeZoneInfo tz) =>
        Enumerable.Range(0, n).Reverse().Select(back => At(nowUtc, tz, -back));

    /// <summary>Linear projection of month-end usage from what was used so far.</summary>
    public static double Project(double usedSoFar, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    {
        var elapsed = (now - start).TotalSeconds;
        var total = (end - start).TotalSeconds;
        return elapsed <= 0 ? usedSoFar : usedSoFar / elapsed * total;
    }

    private static Month At(DateTimeOffset nowUtc, TimeZoneInfo tz, int monthOffset)
    {
        var local = TimeZoneInfo.ConvertTime(nowUtc, tz);
        var first = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(monthOffset);
        return new Month(Local(first, tz), Local(first.AddMonths(1), tz));
    }

    private static DateTimeOffset Local(DateTime unspecified, TimeZoneInfo tz) =>
        new(unspecified, tz.GetUtcOffset(unspecified));
}

public static class Counters
{
    /// <summary>Total increase of a monotonic counter; a drop is a reset and the post-reset value counts in full.</summary>
    public static double Increase(double?[] samples)
    {
        double total = 0;
        double? previous = null;
        foreach (var sample in samples)
        {
            if (sample is not { } v) continue;
            if (previous is { } p) total += v >= p ? v - p : v;
            previous = v;
        }
        return total;
    }

    /// <summary>Increase per bucket of <paramref name="samplesPerBucket"/> consecutive samples (e.g. 24 hourly samples → one day).</summary>
    public static double[] IncreasePerBucket(double?[] samples, int samplesPerBucket)
    {
        if (samples.Length < 2) return [];
        var buckets = (int)Math.Ceiling((samples.Length - 1) / (double)samplesPerBucket);
        var result = new double[buckets];
        for (var b = 0; b < buckets; b++)
        {
            var from = b * samplesPerBucket;
            var to = Math.Min(samples.Length - 1, from + samplesPerBucket);
            result[b] = Increase(samples[from..(to + 1)]);
        }
        return result;
    }
}

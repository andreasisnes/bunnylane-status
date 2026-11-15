using BunnyLane.Status.Metrics;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Metrics;

public class MonthsTests
{
    private static readonly TimeZoneInfo Oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");

    [Fact]
    public void Current_month_is_bounded_in_local_time()
    {
        var (start, end) = Months.Current(DateTimeOffset.Parse("2026-11-15T10:00:00Z"), Oslo);

        start.Should().Be(DateTimeOffset.Parse("2026-11-01T00:00:00+01:00"));
        end.Should().Be(DateTimeOffset.Parse("2026-12-01T00:00:00+01:00"));
    }

    [Fact]
    public void Just_before_utc_midnight_on_the_last_day_is_already_next_month_in_oslo()
    {
        var (start, _) = Months.Current(DateTimeOffset.Parse("2026-11-30T23:30:00Z"), Oslo);

        start.Should().Be(DateTimeOffset.Parse("2026-12-01T00:00:00+01:00"));
    }

    [Fact]
    public void Summer_time_month_uses_the_plus_two_offset()
    {
        var (start, end) = Months.Current(DateTimeOffset.Parse("2026-07-10T12:00:00Z"), Oslo);

        start.Should().Be(DateTimeOffset.Parse("2026-07-01T00:00:00+02:00"));
        end.Should().Be(DateTimeOffset.Parse("2026-08-01T00:00:00+02:00"));
    }

    [Fact]
    public void Last_six_months_end_with_the_current_one_and_cross_the_year()
    {
        var months = Months.Last(6, DateTimeOffset.Parse("2026-02-10T12:00:00Z"), Oslo).ToList();

        months.Should().HaveCount(6);
        months[0].Start.Should().Be(DateTimeOffset.Parse("2025-09-01T00:00:00+02:00"));
        months[^1].Start.Should().Be(DateTimeOffset.Parse("2026-02-01T00:00:00+01:00"));
    }

    [Fact]
    public void Counter_increase_tolerates_resets()
    {
        Counters.Increase([100, 150, 20, 60]).Should().Be(110);
        Counters.Increase([100, null, 130]).Should().Be(30);
        Counters.Increase([]).Should().Be(0);
        Counters.Increase([5]).Should().Be(0);
    }

    [Fact]
    public void Projection_scales_used_by_elapsed_fraction_of_the_month()
    {
        var start = DateTimeOffset.Parse("2026-11-01T00:00:00+01:00");
        var end = DateTimeOffset.Parse("2026-12-01T00:00:00+01:00");
        var now = DateTimeOffset.Parse("2026-11-16T00:00:00+01:00");

        Months.Project(2.31e12, start, end, now).Should().BeApproximately(4.62e12, 1e6);
    }
}

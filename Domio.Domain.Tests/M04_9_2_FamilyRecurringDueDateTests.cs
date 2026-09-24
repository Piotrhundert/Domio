using Domio.Domain.FamilyFinance;

namespace Domio.Domain.Tests;

public sealed class M04_9_2_FamilyRecurringDueDateTests
{
    [Fact]
    public void End_of_month_should_follow_calendar_length()
    {
        Assert.Equal(
            new DateTime(2028, 2, 29, 0, 0, 0, DateTimeKind.Utc),
            FamilyRecurringDueDateModes.ResolveDateUtc(
                FamilyRecurringDueDateModes.EndOfMonth,
                31,
                null,
                2028,
                2));
    }

    [Fact]
    public void Days_before_end_should_be_calculated_for_each_month()
    {
        Assert.Equal(
            new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc),
            FamilyRecurringDueDateModes.ResolveDateUtc(
                FamilyRecurringDueDateModes.DaysBeforeEnd,
                31,
                3,
                2026,
                9));

        Assert.Equal(
            new DateTime(2026, 2, 25, 0, 0, 0, DateTimeKind.Utc),
            FamilyRecurringDueDateModes.ResolveDateUtc(
                FamilyRecurringDueDateModes.DaysBeforeEnd,
                31,
                3,
                2026,
                2));
    }

    [Fact]
    public void Specific_day_31_should_be_clamped_in_short_month()
    {
        Assert.Equal(
            new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc),
            FamilyRecurringDueDateModes.ResolveDateUtc(
                FamilyRecurringDueDateModes.SpecificDay,
                31,
                null,
                2026,
                2));
    }
}

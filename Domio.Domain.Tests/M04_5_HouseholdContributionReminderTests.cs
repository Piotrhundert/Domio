using Domio.Domain.HouseholdFinance;

namespace Domio.Domain.Tests;

public sealed class M04_5_HouseholdContributionReminderTests
{
    [Fact]
    public void Reminder_should_be_inactive_before_configured_window()
    {
        var today =
            new DateTime(
                2026,
                9,
                11,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var reminder =
            HouseholdContributionReminderStates.Resolve(
                today.AddDays(4),
                3,
                10000,
                HouseholdContributionStatuses.Pending,
                today);

        Assert.Equal(
            HouseholdContributionReminderStates.None,
            reminder.Code);

        Assert.False(
            reminder.IsActive);

        Assert.Equal(
            4,
            reminder.DaysToDue);
    }

    [Fact]
    public void Reminder_should_activate_at_configured_upcoming_boundary()
    {
        var today =
            new DateTime(
                2026,
                9,
                11,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var reminder =
            HouseholdContributionReminderStates.Resolve(
                today.AddDays(3),
                3,
                174000,
                HouseholdContributionStatuses.Pending,
                today);

        Assert.Equal(
            HouseholdContributionReminderStates.Upcoming,
            reminder.Code);

        Assert.Equal(
            "Termin za 3 dni",
            reminder.NamePl);

        Assert.True(
            reminder.IsActive);
    }

    [Fact]
    public void Reminder_should_mark_due_today()
    {
        var today =
            new DateTime(
                2026,
                9,
                11,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var reminder =
            HouseholdContributionReminderStates.Resolve(
                today,
                3,
                5000,
                HouseholdContributionStatuses.PartiallyPaid,
                today);

        Assert.Equal(
            HouseholdContributionReminderStates.DueToday,
            reminder.Code);

        Assert.Equal(
            "Termin składki jest dzisiaj",
            reminder.NamePl);

        Assert.Equal(
            0,
            reminder.DaysToDue);
    }

    [Fact]
    public void Reminder_should_mark_overdue_and_keep_days_difference()
    {
        var today =
            new DateTime(
                2026,
                9,
                11,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var reminder =
            HouseholdContributionReminderStates.Resolve(
                today.AddDays(-2),
                3,
                2500,
                HouseholdContributionStatuses.Overdue,
                today);

        Assert.Equal(
            HouseholdContributionReminderStates.Overdue,
            reminder.Code);

        Assert.Equal(
            "Po terminie o 2 dni",
            reminder.NamePl);

        Assert.Equal(
            -2,
            reminder.DaysToDue);
    }

    [Theory]
    [InlineData(HouseholdContributionStatuses.Paid)]
    [InlineData(HouseholdContributionStatuses.Cancelled)]
    [InlineData(HouseholdContributionStatuses.Corrected)]
    public void Reminder_should_be_inactive_for_closed_obligation(
        string statusCode)
    {
        var today =
            new DateTime(
                2026,
                9,
                11,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var reminder =
            HouseholdContributionReminderStates.Resolve(
                today.AddDays(-10),
                3,
                10000,
                statusCode,
                today);

        Assert.Equal(
            HouseholdContributionReminderStates.None,
            reminder.Code);

        Assert.False(
            reminder.IsActive);
    }

    [Fact]
    public void Reminder_should_be_inactive_when_nothing_is_outstanding()
    {
        var today =
            new DateTime(
                2026,
                9,
                11,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var reminder =
            HouseholdContributionReminderStates.Resolve(
                today,
                3,
                0,
                HouseholdContributionStatuses.Pending,
                today);

        Assert.Equal(
            HouseholdContributionReminderStates.None,
            reminder.Code);

        Assert.False(
            reminder.IsActive);
    }
}

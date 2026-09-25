using System.Data;
using System.Data.Common;
using Domio.Application.Notifications;
using Domio.Domain.Notifications;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Notifications;

public sealed class NotificationDeliveryOrchestratorService(
    DomioDbContext dbContext,
    INotificationService notificationService,
    NotificationSettingsService settingsService)
    : INotificationDeliveryOrchestratorService
{
    private static readonly string[] LegacyUpcomingEventCodes =
    [
        "M04.10.2.ContributionUpcoming",
        "M04.10.2.MemberObligationUpcoming",
        "M04.10.3.InvoiceUpcoming",
        "M04.10.4.ChildContributionUpcoming",
        "M04.10.4.FinancialGoalUpcoming"
    ];

    public async Task ProcessUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var settings =
            await settingsService.GetUserSettingsAsync(
                userId,
                cancellationToken);

        await GeneratePersonalizedRemindersAsync(
            userId,
            settings,
            cancellationToken);

        await QueueEmailDeliveriesAsync(
            userId,
            settings,
            cancellationToken);

        await ApplyInAppVisibilityAsync(
            userId,
            settings,
            cancellationToken);
    }

    private async Task GeneratePersonalizedRemindersAsync(
        Guid userId,
        NotificationUserSettings settings,
        CancellationToken cancellationToken)
    {
        var context =
            await GetUserContextAsync(
                userId,
                cancellationToken);

        if (context is null)
        {
            return;
        }

        var today =
            DateTime.UtcNow.Date;

        var dueItems =
            await ReadDueItemsAsync(
                context,
                today,
                today.AddDays(8),
                cancellationToken);

        foreach (var item in dueItems)
        {
            var daysToDue =
                (item.DueDateUtc.Date -
                 today)
                .Days;

            if (daysToDue <= 0)
            {
                continue;
            }

            var threshold =
                ResolveReminderThreshold(
                    settings,
                    daysToDue);

            if (!threshold.HasValue)
            {
                continue;
            }

            var remaining =
                item.RemainingMinor /
                100m;

            var dayText =
                daysToDue == 1
                    ? "jutro"
                    : $"za {daysToDue} dni";

            await notificationService.PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    "M04.10.5.PersonalizedDueReminder",
                    item.CategoryCode,
                    NotificationSeverityCodes.Important,
                    item.Title,
                    $"{item.Label}: {remaining:N2} zł · termin {dayText} ({item.DueDateUtc.ToLocalTime():dd.MM.yyyy}).",
                    true,
                    item.LinkUrl,
                    item.SourceType,
                    item.SourceId.ToString(),
                    $"M04.10.5:reminder:{item.SourceType}:{item.SourceId:D}:{threshold.Value}"),
                cancellationToken);
        }
    }

    private async Task QueueEmailDeliveriesAsync(
        Guid userId,
        NotificationUserSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.EmailEnabled ||
            !settings.EmailEnabledAtUtc.HasValue)
        {
            return;
        }

        var emailConfiguration =
            await settingsService
                .GetEmailConfigurationForDeliveryAsync(
                    cancellationToken);

        if (!emailConfiguration.IsEnabled ||
            string.IsNullOrWhiteSpace(
                emailConfiguration.SenderEmail) ||
            string.IsNullOrWhiteSpace(
                emailConfiguration.SmtpHost))
        {
            return;
        }

        var recipientEmail =
            !string.IsNullOrWhiteSpace(
                settings.EmailAddress)
                ? settings.EmailAddress
                : await dbContext.UserAccounts
                    .AsNoTracking()
                    .Where(x =>
                        x.Id == userId &&
                        x.IsActive)
                    .Select(x =>
                        x.Email)
                    .SingleOrDefaultAsync(
                        cancellationToken);

        if (string.IsNullOrWhiteSpace(
                recipientEmail))
        {
            return;
        }

        var oldest =
            DateTime.UtcNow.AddDays(-7);

        var createdFromUtc =
            settings.EmailEnabledAtUtc.Value >
            oldest
                ? settings.EmailEnabledAtUtc.Value
                : oldest;

        var candidates =
            await ReadEmailCandidatesAsync(
                userId,
                createdFromUtc,
                cancellationToken);

        foreach (var item in candidates)
        {
            if (!IsCategoryEnabled(
                    settings,
                    item.CategoryCode) ||
                !ShouldDeliverEventByEmail(
                    settings,
                    item.EventCode))
            {
                continue;
            }

            var template =
                await settingsService
                    .GetMessageTemplateForDeliveryAsync(
                        item.CategoryCode,
                        cancellationToken);

            var link =
                BuildNotificationLink(
                    item.LinkUrl,
                    emailConfiguration.ApplicationBaseUrl);

            var renderedSubject =
                RenderTemplate(
                    template.SubjectTemplate,
                    item,
                    link);

            var renderedBody =
                RenderTemplate(
                    template.BodyTemplate,
                    item,
                    link);

            var subject =
                ClampText(
                    string.IsNullOrWhiteSpace(
                        renderedSubject)
                        ? $"Domio · {item.Title}"
                        : renderedSubject.Trim(),
                    300);

            var body =
                ClampText(
                    string.IsNullOrWhiteSpace(
                        renderedBody)
                        ? item.Message
                        : renderedBody.Trim(),
                    4000);

            await ExecuteAsync(
                """
                INSERT OR IGNORE INTO NotificationEmailDeliveries
                    (Id, NotificationId, RecipientUserId,
                     RecipientEmail, Subject, Body,
                     StatusCode, AttemptCount,
                     CreatedAtUtc, LastAttemptAtUtc,
                     SentAtUtc, LastError, IsTest)
                VALUES
                    ($id, $notificationId, $recipientUserId,
                     $recipientEmail, $subject, $body,
                     'Pending', 0,
                     $createdAtUtc, NULL,
                     NULL, NULL, 0);
                """,
                [
                    P("$id", Guid.NewGuid()),
                    P("$notificationId", item.NotificationId),
                    P("$recipientUserId", userId),
                    P("$recipientEmail", recipientEmail.Trim()),
                    P("$subject", subject),
                    P("$body", body),
                    P("$createdAtUtc", DateTime.UtcNow)
                ],
                cancellationToken);
        }
    }

    private async Task ApplyInAppVisibilityAsync(
        Guid userId,
        NotificationUserSettings settings,
        CancellationToken cancellationToken)
    {
        foreach (var eventCode in
                 LegacyUpcomingEventCodes)
        {
            await HideByEventCodeAsync(
                userId,
                eventCode,
                cancellationToken);
        }

        if (!settings.ReminderDueDay)
        {
            await HideByEventSuffixAsync(
                userId,
                "DueToday",
                cancellationToken);
        }

        if (!settings.ReminderOverdue)
        {
            await HideByEventSuffixAsync(
                userId,
                "Overdue",
                cancellationToken);
        }

        if (!settings.InAppEnabled)
        {
            await ExecuteAsync(
                """
                UPDATE UserNotifications
                SET ShowInInbox = 0
                WHERE UserId = $userId
                  AND ShowInInbox = 1;
                """,
                [P("$userId", userId)],
                cancellationToken);

            return;
        }

        foreach (var categoryCode in
                 GetDisabledCategories(
                     settings))
        {
            await ExecuteAsync(
                """
                UPDATE UserNotifications
                SET ShowInInbox = 0
                WHERE UserId = $userId
                  AND CategoryCode = $categoryCode
                  AND ShowInInbox = 1;
                """,
                [
                    P("$userId", userId),
                    P("$categoryCode", categoryCode)
                ],
                cancellationToken);
        }
    }

    private async Task HideByEventCodeAsync(
        Guid userId,
        string eventCode,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            """
            UPDATE UserNotifications
            SET ShowInInbox = 0
            WHERE UserId = $userId
              AND EventCode = $eventCode
              AND ShowInInbox = 1;
            """,
            [
                P("$userId", userId),
                P("$eventCode", eventCode)
            ],
            cancellationToken);
    }

    private async Task HideByEventSuffixAsync(
        Guid userId,
        string suffix,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            """
            UPDATE UserNotifications
            SET ShowInInbox = 0
            WHERE UserId = $userId
              AND EventCode LIKE $pattern
              AND ShowInInbox = 1;
            """,
            [
                P("$userId", userId),
                P("$pattern", $"%{suffix}")
            ],
            cancellationToken);
    }

    private async Task<UserContext?> GetUserContextAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var personId =
            await (
                from account in dbContext.UserAccounts
                    .AsNoTracking()
                join person in dbContext.People
                    .AsNoTracking()
                    on account.PersonId equals person.Id
                where
                    account.Id == userId &&
                    account.IsActive &&
                    person.IsActive
                select (Guid?)person.Id)
            .SingleOrDefaultAsync(
                cancellationToken);

        if (!personId.HasValue)
        {
            return null;
        }

        var membership =
            await dbContext.HouseholdMembers
                .AsNoTracking()
                .Where(x =>
                    x.PersonId == personId.Value &&
                    x.IsActive)
                .OrderByDescending(x =>
                    x.JoinedAtUtc)
                .Select(x =>
                    new
                    {
                        x.Id,
                        x.HouseholdId
                    })
                .FirstOrDefaultAsync(
                    cancellationToken);

        return new UserContext(
            userId,
            personId.Value,
            membership?.Id,
            membership?.HouseholdId);
    }

    private async Task<IReadOnlyList<DueItem>>
        ReadDueItemsAsync(
            UserContext context,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken)
    {
        var result =
            new List<DueItem>();

        await WithConnectionAsync(
            async connection =>
            {
                if (context.HouseholdMemberId.HasValue)
                {
                    await ReadOwnContributionDueItemsAsync(
                        connection,
                        context.HouseholdMemberId.Value,
                        fromUtc,
                        toUtc,
                        result,
                        cancellationToken);

                    await ReadMemberObligationDueItemsAsync(
                        connection,
                        context.HouseholdMemberId.Value,
                        fromUtc,
                        toUtc,
                        result,
                        cancellationToken);
                }

                if (context.HouseholdId.HasValue)
                {
                    await ReadInvoiceDueItemsAsync(
                        connection,
                        context.HouseholdId.Value,
                        fromUtc,
                        toUtc,
                        result,
                        cancellationToken);
                }

                await ReadChildContributionDueItemsAsync(
                    connection,
                    context.PersonId,
                    fromUtc,
                    toUtc,
                    result,
                    cancellationToken);

                await ReadGoalDueItemsAsync(
                    connection,
                    context,
                    fromUtc,
                    toUtc,
                    result,
                    cancellationToken);
            },
            cancellationToken);

        return result;
    }

    private static async Task ReadOwnContributionDueItemsAsync(
        DbConnection connection,
        Guid householdMemberId,
        DateTime fromUtc,
        DateTime toUtc,
        List<DueItem> result,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT Id, PeriodKey, DueDateUtc,
                   AmountMinor - PaidAmountMinor
            FROM HouseholdContributionObligations
            WHERE HouseholdMemberId = $memberId
              AND DueDateUtc >= $fromUtc
              AND DueDateUtc < $toUtc
              AND StatusCode NOT IN ('Paid', 'Cancelled', 'Corrected')
              AND AmountMinor > PaidAmountMinor;
            """;

        AddParameter(command, "$memberId", householdMemberId);
        AddParameter(command, "$fromUtc", fromUtc);
        AddParameter(command, "$toUtc", toUtc);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
            cancellationToken))
        {
            result.Add(
                new DueItem(
                    reader.GetGuid(0),
                    "HouseholdContributionObligation",
                    NotificationCategoryCodes.Contribution,
                    "Zbliża się termin składki",
                    $"Składka za {reader.GetString(1)}",
                    ReadDateTime(reader, 2),
                    reader.GetInt64(3),
                    "/HouseholdFinance/Contributions"));
        }
    }

    private static async Task ReadMemberObligationDueItemsAsync(
        DbConnection connection,
        Guid householdMemberId,
        DateTime fromUtc,
        DateTime toUtc,
        List<DueItem> result,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT Id, Description, DueDateUtc,
                   AmountMinor - PaidAmountMinor
            FROM HouseholdMemberObligations
            WHERE HouseholdMemberId = $memberId
              AND DueDateUtc >= $fromUtc
              AND DueDateUtc < $toUtc
              AND StatusCode <> 'Cancelled'
              AND AmountMinor > PaidAmountMinor;
            """;

        AddParameter(command, "$memberId", householdMemberId);
        AddParameter(command, "$fromUtc", fromUtc);
        AddParameter(command, "$toUtc", toUtc);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
            cancellationToken))
        {
            result.Add(
                new DueItem(
                    reader.GetGuid(0),
                    "HouseholdMemberObligation",
                    NotificationCategoryCodes.Household,
                    "Zbliża się termin zobowiązania",
                    reader.GetString(1),
                    ReadDateTime(reader, 2),
                    reader.GetInt64(3),
                    "/HouseholdFinance/MemberObligations"));
        }
    }

    private static async Task ReadInvoiceDueItemsAsync(
        DbConnection connection,
        Guid householdId,
        DateTime fromUtc,
        DateTime toUtc,
        List<DueItem> result,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT invoice.Id, invoice.Supplier, invoice.InvoiceNumber,
                   invoice.DueDateUtc,
                   invoice.GrossAmountMinor -
                       COALESCE((
                           SELECT SUM(payment.AmountMinor)
                           FROM HouseholdInvoicePayments payment
                           WHERE payment.InvoiceId = invoice.Id
                       ), 0)
            FROM HouseholdInvoices invoice
            WHERE invoice.HouseholdId = $householdId
              AND invoice.DueDateUtc >= $fromUtc
              AND invoice.DueDateUtc < $toUtc
              AND invoice.StatusCode <> 'Cancelled'
              AND invoice.GrossAmountMinor >
                  COALESCE((
                      SELECT SUM(payment.AmountMinor)
                      FROM HouseholdInvoicePayments payment
                      WHERE payment.InvoiceId = invoice.Id
                  ), 0);
            """;

        AddParameter(command, "$householdId", householdId);
        AddParameter(command, "$fromUtc", fromUtc);
        AddParameter(command, "$toUtc", toUtc);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
            cancellationToken))
        {
            result.Add(
                new DueItem(
                    reader.GetGuid(0),
                    "HouseholdInvoice",
                    NotificationCategoryCodes.Invoice,
                    "Zbliża się termin faktury",
                    $"{reader.GetString(1)} · {reader.GetString(2)}",
                    ReadDateTime(reader, 3),
                    reader.GetInt64(4),
                    "/HouseholdFinance/Invoices"));
        }
    }

    private static async Task ReadChildContributionDueItemsAsync(
        DbConnection connection,
        Guid userPersonId,
        DateTime fromUtc,
        DateTime toUtc,
        List<DueItem> result,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT DISTINCT obligation.Id, obligation.PeriodKey,
                   obligation.DueDateUtc,
                   obligation.AmountMinor - obligation.PaidAmountMinor,
                   child.DisplayName, child.FirstName, child.LastName,
                   family.Id
            FROM FamilyMembers ownMembership
            INNER JOIN FamilyGroups family
                ON family.Id = ownMembership.FamilyGroupId
            INNER JOIN FamilyMembers childMembership
                ON childMembership.FamilyGroupId = family.Id
            INNER JOIN HouseholdMembers householdChild
                ON householdChild.PersonId = childMembership.PersonId
               AND householdChild.HouseholdId = family.HouseholdId
            INNER JOIN People child
                ON child.Id = childMembership.PersonId
            INNER JOIN HouseholdContributionObligations obligation
                ON obligation.HouseholdMemberId = householdChild.Id
            INNER JOIN HouseholdContributionRules rule
                ON rule.Id = obligation.ContributionRuleId
            WHERE ownMembership.PersonId = $personId
              AND ownMembership.ValidToUtc IS NULL
              AND childMembership.ValidToUtc IS NULL
              AND childMembership.FamilyRoleCode = 'Child'
              AND householdChild.IsActive = 1
              AND rule.IncomeRuleId IS NULL
              AND obligation.DueDateUtc >= $fromUtc
              AND obligation.DueDateUtc < $toUtc
              AND obligation.StatusCode NOT IN ('Paid', 'Cancelled', 'Corrected')
              AND obligation.AmountMinor > obligation.PaidAmountMinor;
            """;

        AddParameter(command, "$personId", userPersonId);
        AddParameter(command, "$fromUtc", fromUtc);
        AddParameter(command, "$toUtc", toUtc);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
            cancellationToken))
        {
            var displayName =
                BuildDisplayName(
                    reader.IsDBNull(4)
                        ? null
                        : reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6));

            var familyGroupId =
                reader.GetGuid(7);

            result.Add(
                new DueItem(
                    reader.GetGuid(0),
                    "FamilyChildContribution",
                    NotificationCategoryCodes.Family,
                    "Zbliża się termin składki dziecka",
                    $"{displayName} · składka za {reader.GetString(1)}",
                    ReadDateTime(reader, 2),
                    reader.GetInt64(3),
                    $"/FamilyFinance?familyGroupId={familyGroupId:D}&tab=child-contributions"));
        }
    }

    private static async Task ReadGoalDueItemsAsync(
        DbConnection connection,
        UserContext context,
        DateTime fromUtc,
        DateTime toUtc,
        List<DueItem> result,
        CancellationToken cancellationToken)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT goal.Id, goal.Name, goal.TargetDateUtc,
                   goal.TargetAmountMinor -
                       COALESCE((
                           SELECT SUM(contribution.AmountMinor)
                           FROM FinancialGoalContributions contribution
                           WHERE contribution.GoalId = goal.Id
                       ), 0)
            FROM FinancialGoals goal
            WHERE goal.IsActive = 1
              AND goal.TargetDateUtc IS NOT NULL
              AND goal.TargetDateUtc >= $fromUtc
              AND goal.TargetDateUtc < $toUtc
              AND goal.TargetAmountMinor >
                  COALESCE((
                      SELECT SUM(contribution.AmountMinor)
                      FROM FinancialGoalContributions contribution
                      WHERE contribution.GoalId = goal.Id
                  ), 0)
              AND (
                    (goal.ScopeCode = 'Personal'
                     AND goal.OwnerPersonId = $personId)
                 OR (goal.ScopeCode = 'Household'
                     AND goal.HouseholdId = $householdId)
                 OR (goal.ScopeCode = 'Family'
                     AND goal.FamilyGroupId IN (
                         SELECT membership.FamilyGroupId
                         FROM FamilyMembers membership
                         WHERE membership.PersonId = $personId
                           AND membership.ValidToUtc IS NULL
                     ))
              );
            """;

        AddParameter(command, "$personId", context.PersonId);
        AddParameter(command, "$householdId", context.HouseholdId);
        AddParameter(command, "$fromUtc", fromUtc);
        AddParameter(command, "$toUtc", toUtc);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
            cancellationToken))
        {
            var id =
                reader.GetGuid(0);

            result.Add(
                new DueItem(
                    id,
                    "FinancialGoal",
                    NotificationCategoryCodes.Goal,
                    "Zbliża się termin celu finansowego",
                    $"Cel „{reader.GetString(1)}”",
                    ReadDateTime(reader, 2),
                    reader.GetInt64(3),
                    $"/FinancialGoals/Details/{id:D}"));
        }
    }

    private async Task<IReadOnlyList<EmailCandidate>>
        ReadEmailCandidatesAsync(
            Guid userId,
            DateTime createdFromUtc,
            CancellationToken cancellationToken)
    {
        var result =
            new List<EmailCandidate>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT notification.Id, notification.EventCode,
                           notification.CategoryCode, notification.Title,
                           notification.Message, notification.LinkUrl
                    FROM UserNotifications notification
                    WHERE notification.UserId = $userId
                      AND notification.ShowInInbox = 1
                      AND notification.CreatedAtUtc >= $createdFromUtc
                      AND NOT EXISTS (
                          SELECT 1
                          FROM NotificationEmailDeliveries delivery
                          WHERE delivery.NotificationId = notification.Id
                      )
                    ORDER BY notification.CreatedAtUtc;
                    """;

                AddParameter(command, "$userId", userId);
                AddParameter(command, "$createdFromUtc", createdFromUtc);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new EmailCandidate(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetString(4),
                            reader.IsDBNull(5)
                                ? null
                                : reader.GetString(5)));
                }
            },
            cancellationToken);

        return result;
    }

    private static string RenderTemplate(
        string template,
        EmailCandidate item,
        string link)
    {
        var result =
            template
                .Replace(
                    "{Title}",
                    item.Title,
                    StringComparison.Ordinal)
                .Replace(
                    "{Message}",
                    item.Message,
                    StringComparison.Ordinal)
                .Replace(
                    "{Category}",
                    NotificationCategoryCodes.GetNamePl(
                        item.CategoryCode),
                    StringComparison.Ordinal)
                .Replace(
                    "{Link}",
                    link,
                    StringComparison.Ordinal)
                .Replace(
                    "{EventCode}",
                    item.EventCode,
                    StringComparison.Ordinal)
                .Replace(
                    "{Date}",
                    DateTime.Now.ToString(
                        "dd.MM.yyyy HH:mm"),
                    StringComparison.Ordinal);

        return NormalizeBlankLines(
            result);
    }

    private static string BuildNotificationLink(
        string? linkUrl,
        string? applicationBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(
                linkUrl))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(
                applicationBaseUrl))
        {
            return "Otwórz w Domio: " +
                applicationBaseUrl
                    .TrimEnd('/') +
                linkUrl;
        }

        return "Otwórz w Domio: " +
            linkUrl;
    }

    private static string NormalizeBlankLines(
        string value)
    {
        var normalized =
            value.Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal);

        while (normalized.Contains(
            "\n\n\n",
            StringComparison.Ordinal))
        {
            normalized =
                normalized.Replace(
                    "\n\n\n",
                    "\n\n",
                    StringComparison.Ordinal);
        }

        return normalized
            .Replace(
                "\n",
                Environment.NewLine,
                StringComparison.Ordinal)
            .Trim();
    }

    private static string ClampText(
        string value,
        int maxLength) =>
        value.Length <= maxLength
            ? value
            : value[..maxLength];

    private static int? ResolveReminderThreshold(
        NotificationUserSettings settings,
        int daysToDue)
    {
        if (daysToDue <= 0)
        {
            return null;
        }

        var enabled =
            new List<int>(3);

        if (settings.Reminder1Day)
        {
            enabled.Add(1);
        }

        if (settings.Reminder3Days)
        {
            enabled.Add(3);
        }

        if (settings.Reminder7Days)
        {
            enabled.Add(7);
        }

        return enabled
            .Where(x =>
                daysToDue <= x)
            .OrderBy(x => x)
            .Cast<int?>()
            .FirstOrDefault();
    }

    private static bool IsCategoryEnabled(
        NotificationUserSettings settings,
        string categoryCode) =>
        categoryCode switch
        {
            NotificationCategoryCodes.System =>
                settings.NotifySystem,
            NotificationCategoryCodes.PersonalFinance =>
                settings.NotifyPersonalFinance,
            NotificationCategoryCodes.Household =>
                settings.NotifyHousehold,
            NotificationCategoryCodes.Contribution =>
                settings.NotifyContributions,
            NotificationCategoryCodes.Invoice =>
                settings.NotifyInvoices,
            NotificationCategoryCodes.Family =>
                settings.NotifyFamily,
            NotificationCategoryCodes.Goal =>
                settings.NotifyGoals,
            NotificationCategoryCodes.User =>
                settings.NotifyUsers,
            _ =>
                true
        };

    private static IReadOnlyList<string> GetDisabledCategories(
        NotificationUserSettings settings)
    {
        var result =
            new List<string>();

        void AddIfDisabled(
            bool enabled,
            string code)
        {
            if (!enabled)
            {
                result.Add(code);
            }
        }

        AddIfDisabled(settings.NotifySystem, NotificationCategoryCodes.System);
        AddIfDisabled(settings.NotifyPersonalFinance, NotificationCategoryCodes.PersonalFinance);
        AddIfDisabled(settings.NotifyHousehold, NotificationCategoryCodes.Household);
        AddIfDisabled(settings.NotifyContributions, NotificationCategoryCodes.Contribution);
        AddIfDisabled(settings.NotifyInvoices, NotificationCategoryCodes.Invoice);
        AddIfDisabled(settings.NotifyFamily, NotificationCategoryCodes.Family);
        AddIfDisabled(settings.NotifyGoals, NotificationCategoryCodes.Goal);
        AddIfDisabled(settings.NotifyUsers, NotificationCategoryCodes.User);

        return result;
    }

    private static bool ShouldDeliverEventByEmail(
        NotificationUserSettings settings,
        string eventCode)
    {
        if (LegacyUpcomingEventCodes.Contains(
                eventCode,
                StringComparer.Ordinal))
        {
            return false;
        }

        if (eventCode.EndsWith(
                "DueToday",
                StringComparison.Ordinal) &&
            !settings.ReminderDueDay)
        {
            return false;
        }

        if (eventCode.EndsWith(
                "Overdue",
                StringComparison.Ordinal) &&
            !settings.ReminderOverdue)
        {
            return false;
        }

        return true;
    }

    private async Task<int> ExecuteAsync(
        string sql,
        IReadOnlyList<ParameterValue> parameters,
        CancellationToken cancellationToken)
    {
        var result = 0;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText = sql;

                foreach (var parameter in parameters)
                {
                    AddParameter(
                        command,
                        parameter.Name,
                        parameter.Value);
                }

                result =
                    await command.ExecuteNonQueryAsync(
                        cancellationToken);
            },
            cancellationToken);

        return result;
    }

    private async Task WithConnectionAsync(
        Func<DbConnection, Task> action,
        CancellationToken cancellationToken)
    {
        var connection =
            dbContext.Database.GetDbConnection();

        var shouldClose =
            connection.State !=
            ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(
                cancellationToken);
        }

        try
        {
            await action(
                connection);
        }
        finally
        {
            if (shouldClose &&
                dbContext.Database.CurrentTransaction is null)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object? value)
    {
        var parameter =
            command.CreateParameter();

        parameter.ParameterName =
            name;

        parameter.Value =
            value ??
            DBNull.Value;

        command.Parameters.Add(
            parameter);
    }

    private static ParameterValue P(
        string name,
        object? value) =>
        new(
            name,
            value);

    private static DateTime ReadDateTime(
        DbDataReader reader,
        int ordinal)
    {
        var value =
            reader.GetValue(
                ordinal);

        if (value is DateTime dateTime)
        {
            return dateTime.Kind ==
                   DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(
                    dateTime,
                    DateTimeKind.Utc)
                : dateTime.ToUniversalTime();
        }

        return DateTime.SpecifyKind(
            Convert.ToDateTime(
                value),
            DateTimeKind.Utc);
    }

    private static string BuildDisplayName(
        string? displayName,
        string firstName,
        string lastName) =>
        string.IsNullOrWhiteSpace(
            displayName)
            ? $"{firstName} {lastName}".Trim()
            : displayName.Trim();

    private sealed record UserContext(
        Guid UserId,
        Guid PersonId,
        Guid? HouseholdMemberId,
        Guid? HouseholdId);

    private sealed record DueItem(
        Guid SourceId,
        string SourceType,
        string CategoryCode,
        string Title,
        string Label,
        DateTime DueDateUtc,
        long RemainingMinor,
        string LinkUrl);

    private sealed record EmailCandidate(
        Guid NotificationId,
        string EventCode,
        string CategoryCode,
        string Title,
        string Message,
        string? LinkUrl);

    private sealed record ParameterValue(
        string Name,
        object? Value);
}

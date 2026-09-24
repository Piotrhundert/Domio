using System.Data;
using System.Data.Common;
using Domio.Application.Notifications;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.Notifications;
using Domio.Domain.Users;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Notifications;

public sealed class NotificationService(
    DomioDbContext dbContext) : INotificationService
{
    public async Task<NotificationHeaderSummary> GetHeaderAsync(
        Guid userId,
        int take = 5,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewAsync(
            userId,
            cancellationToken);

        await EnsureFoundationNotificationAsync(
            userId,
            cancellationToken);

        await EnsureHouseholdOperationalNotificationsAsync(
            userId,
            cancellationToken);

        take = Math.Clamp(take, 1, 20);

        var unreadCount =
            await ScalarLongAsync(
                """
                SELECT COUNT(1)
                FROM UserNotifications
                WHERE UserId = $userId
                  AND ShowInInbox = 1
                  AND IsRead = 0;
                """,
                [P("$userId", userId)],
                cancellationToken);

        var recent =
            await ReadItemsAsync(
                """
                SELECT Id, EventCode, CategoryCode, SeverityCode,
                       Title, Message, LinkUrl, SourceType, SourceId,
                       ShowInInbox, IsRead, ReadAtUtc, CreatedAtUtc
                FROM UserNotifications
                WHERE UserId = $userId
                  AND ShowInInbox = 1
                ORDER BY IsRead ASC, CreatedAtUtc DESC
                LIMIT $take;
                """,
                [
                    P("$userId", userId),
                    P("$take", take)
                ],
                cancellationToken);

        return new NotificationHeaderSummary(
            checked((int)unreadCount),
            recent);
    }

    public async Task<NotificationCenterOverview> GetOverviewAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewAsync(
            userId,
            cancellationToken);

        await EnsureFoundationNotificationAsync(
            userId,
            cancellationToken);

        await EnsureHouseholdOperationalNotificationsAsync(
            userId,
            cancellationToken);

        var unreadCount =
            await ScalarLongAsync(
                """
                SELECT COUNT(1)
                FROM UserNotifications
                WHERE UserId = $userId
                  AND ShowInInbox = 1
                  AND IsRead = 0;
                """,
                [P("$userId", userId)],
                cancellationToken);

        var importantUnread =
            await ScalarLongAsync(
                """
                SELECT COUNT(1)
                FROM UserNotifications
                WHERE UserId = $userId
                  AND ShowInInbox = 1
                  AND IsRead = 0
                  AND SeverityCode = $severity;
                """,
                [
                    P("$userId", userId),
                    P("$severity", NotificationSeverityCodes.Important)
                ],
                cancellationToken);

        var warningUnread =
            await ScalarLongAsync(
                """
                SELECT COUNT(1)
                FROM UserNotifications
                WHERE UserId = $userId
                  AND ShowInInbox = 1
                  AND IsRead = 0
                  AND SeverityCode = $severity;
                """,
                [
                    P("$userId", userId),
                    P("$severity", NotificationSeverityCodes.Warning)
                ],
                cancellationToken);

        var inbox =
            await ReadItemsAsync(
                """
                SELECT Id, EventCode, CategoryCode, SeverityCode,
                       Title, Message, LinkUrl, SourceType, SourceId,
                       ShowInInbox, IsRead, ReadAtUtc, CreatedAtUtc
                FROM UserNotifications
                WHERE UserId = $userId
                  AND ShowInInbox = 1
                ORDER BY IsRead ASC, CreatedAtUtc DESC
                LIMIT 200;
                """,
                [P("$userId", userId)],
                cancellationToken);

        var activity =
            await ReadItemsAsync(
                """
                SELECT Id, EventCode, CategoryCode, SeverityCode,
                       Title, Message, LinkUrl, SourceType, SourceId,
                       ShowInInbox, IsRead, ReadAtUtc, CreatedAtUtc
                FROM UserNotifications
                WHERE UserId = $userId
                ORDER BY CreatedAtUtc DESC
                LIMIT 300;
                """,
                [P("$userId", userId)],
                cancellationToken);

        return new NotificationCenterOverview(
            checked((int)unreadCount),
            checked((int)importantUnread),
            checked((int)warningUnread),
            inbox,
            activity);
    }

    public async Task<int> PublishAsync(
        PublishNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RecipientUserIds);

        if (request.RecipientUserIds.Count == 0)
        {
            return 0;
        }

        if (!NotificationCategoryCodes.IsValid(
                request.CategoryCode))
        {
            throw new ArgumentException(
                "Nieprawidłowa kategoria powiadomienia.");
        }

        if (!NotificationSeverityCodes.IsValid(
                request.SeverityCode))
        {
            throw new ArgumentException(
                "Nieprawidłowy poziom powiadomienia.");
        }

        var eventCode =
            NormalizeRequired(
                request.EventCode,
                "Kod zdarzenia",
                100);

        var title =
            NormalizeRequired(
                request.Title,
                "Tytuł",
                180);

        var message =
            NormalizeRequired(
                request.Message,
                "Treść",
                1000);

        var linkUrl =
            NormalizeOptional(
                request.LinkUrl,
                500);

        if (!string.IsNullOrWhiteSpace(linkUrl) &&
            (!linkUrl.StartsWith("/", StringComparison.Ordinal) ||
             linkUrl.StartsWith("//", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Odnośnik powiadomienia musi być lokalną ścieżką aplikacji.");
        }

        var sourceType =
            NormalizeOptional(
                request.SourceType,
                100);

        var sourceId =
            NormalizeOptional(
                request.SourceId,
                200);

        var dedupeKey =
            NormalizeOptional(
                request.DedupeKey,
                200);

        var createdAtUtc =
            NormalizeUtc(
                request.CreatedAtUtc ??
                DateTime.UtcNow);

        var recipientIds =
            request.RecipientUserIds
                .Distinct()
                .ToArray();

        var activeRecipientIds =
            await dbContext.UserAccounts
                .AsNoTracking()
                .Where(x =>
                    recipientIds.Contains(x.Id) &&
                    x.IsActive)
                .Select(x => x.Id)
                .ToArrayAsync(cancellationToken);

        var inserted = 0;

        foreach (var recipientUserId in activeRecipientIds)
        {
            inserted +=
                await ExecuteAsync(
                    """
                    INSERT OR IGNORE INTO UserNotifications
                        (Id, UserId, EventCode, CategoryCode, SeverityCode,
                         Title, Message, LinkUrl, SourceType, SourceId,
                         DedupeKey, ShowInInbox, IsRead, ReadAtUtc, CreatedAtUtc)
                    VALUES
                        ($id, $userId, $eventCode, $categoryCode, $severityCode,
                         $title, $message, $linkUrl, $sourceType, $sourceId,
                         $dedupeKey, $showInInbox, $isRead, $readAtUtc, $createdAtUtc);
                    """,
                    [
                        P("$id", Guid.NewGuid()),
                        P("$userId", recipientUserId),
                        P("$eventCode", eventCode),
                        P("$categoryCode", request.CategoryCode),
                        P("$severityCode", request.SeverityCode),
                        P("$title", title),
                        P("$message", message),
                        P("$linkUrl", linkUrl),
                        P("$sourceType", sourceType),
                        P("$sourceId", sourceId),
                        P("$dedupeKey", dedupeKey),
                        P("$showInInbox", request.ShowInInbox ? 1 : 0),
                        P("$isRead", request.ShowInInbox ? 0 : 1),
                        P(
                            "$readAtUtc",
                            request.ShowInInbox
                                ? null
                                : createdAtUtc),
                        P("$createdAtUtc", createdAtUtc)
                    ],
                    cancellationToken);
        }

        return inserted;
    }

    public async Task MarkReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewAsync(
            userId,
            cancellationToken);

        await ExecuteAsync(
            """
            UPDATE UserNotifications
            SET IsRead = 1,
                ReadAtUtc = COALESCE(ReadAtUtc, $now)
            WHERE Id = $id
              AND UserId = $userId;
            """,
            [
                P("$now", DateTime.UtcNow),
                P("$id", notificationId),
                P("$userId", userId)
            ],
            cancellationToken);
    }

    public async Task MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewAsync(
            userId,
            cancellationToken);

        await ExecuteAsync(
            """
            UPDATE UserNotifications
            SET IsRead = 1,
                ReadAtUtc = COALESCE(ReadAtUtc, $now)
            WHERE UserId = $userId
              AND ShowInInbox = 1
              AND IsRead = 0;
            """,
            [
                P("$now", DateTime.UtcNow),
                P("$userId", userId)
            ],
            cancellationToken);
    }

    public async Task<string?> OpenAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewAsync(
            userId,
            cancellationToken);

        string? linkUrl = null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT LinkUrl
                    FROM UserNotifications
                    WHERE Id = $id
                      AND UserId = $userId
                    LIMIT 1;
                    """;

                AddParameter(
                    command,
                    "$id",
                    notificationId);
                AddParameter(
                    command,
                    "$userId",
                    userId);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                if (await reader.ReadAsync(
                        cancellationToken))
                {
                    linkUrl =
                        reader.IsDBNull(0)
                            ? null
                            : reader.GetString(0);
                }
            },
            cancellationToken);

        await MarkReadAsync(
            notificationId,
            userId,
            cancellationToken);

        return linkUrl;
    }

    private async Task EnsureCanViewAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            userId,
            SystemPermissions.NotificationsView,
            cancellationToken);
    }

    private async Task EnsureFoundationNotificationAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await PublishAsync(
            new PublishNotificationRequest(
                [userId],
                "M04.10.1.NotificationCenterReady",
                NotificationCategoryCodes.System,
                NotificationSeverityCodes.Info,
                "Centrum powiadomień jest aktywne",
                "Domio może teraz gromadzić powiadomienia i historię aktywności. Kolejne paczki podłączą składki, faktury, terminy, rodzinę i cele.",
                true,
                "/Notifications",
                "NotificationCenter",
                "M04.10.1",
                "M04.10.1:NotificationCenterReady"),
            cancellationToken);
    }

    private async Task EnsureHouseholdOperationalNotificationsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var context =
            await GetUserHouseholdContextAsync(
                userId,
                cancellationToken);

        if (context is null)
        {
            return;
        }

        await EnsureContributionRuleNotificationAsync(
            userId,
            context,
            cancellationToken);

        await EnsureContributionObligationNotificationsAsync(
            userId,
            context,
            cancellationToken);

        await EnsureContributionPaymentNotificationsAsync(
            userId,
            context,
            cancellationToken);

        await EnsureMemberObligationNotificationsAsync(
            userId,
            context,
            cancellationToken);

        await EnsureMemberObligationPaymentNotificationsAsync(
            userId,
            context,
            cancellationToken);

        await EnsureInvoiceNotificationsAsync(
            userId,
            context,
            cancellationToken);
    }

    private async Task EnsureContributionRuleNotificationAsync(
        Guid userId,
        UserHouseholdContext context,
        CancellationToken cancellationToken)
    {
        var activeRules =
            await dbContext.HouseholdContributionRules
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == context.HouseholdId &&
                    x.HouseholdMemberId == context.HouseholdMemberId &&
                    x.IsActive)
                .OrderByDescending(x => x.ValidFromUtc)
                .ThenByDescending(x => x.CreatedAtUtc)
                .Take(2)
                .ToArrayAsync(cancellationToken);

        foreach (var rule in activeRules)
        {
            var dueDescription =
                rule.IncomeRuleId.HasValue
                    ? rule.DueOffsetDays == 0
                        ? "w dniu planowanej wypłaty"
                        : $"{rule.DueOffsetDays} dni po planowanej wypłacie"
                    : $"{rule.DueOffsetDays}. dnia miesiąca";

            await PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    "M04.10.2.ContributionRuleActive",
                    NotificationCategoryCodes.Contribution,
                    NotificationSeverityCodes.Info,
                    "Reguła składki została ustawiona lub zmieniona",
                    $"Masz aktywną regułę składki dla domu. Termin: {dueDescription}.",
                    true,
                    "/HouseholdFinance/Contributions",
                    "HouseholdContributionRule",
                    rule.Id.ToString(),
                    $"M04.10.2:contribution-rule:{rule.Id:D}"),
                cancellationToken);
        }
    }

    private async Task EnsureContributionObligationNotificationsAsync(
        Guid userId,
        UserHouseholdContext context,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var windowStart = today.AddMonths(-3);
        var windowEnd = today.AddMonths(2);

        var rows =
            await (
                from obligation in dbContext.HouseholdContributionObligations
                    .AsNoTracking()
                join rule in dbContext.HouseholdContributionRules
                    .AsNoTracking()
                    on obligation.ContributionRuleId equals rule.Id
                where
                    obligation.HouseholdId == context.HouseholdId &&
                    obligation.HouseholdMemberId == context.HouseholdMemberId &&
                    obligation.DueDateUtc >= windowStart &&
                    obligation.DueDateUtc < windowEnd &&
                    obligation.StatusCode != HouseholdContributionStatuses.Cancelled &&
                    obligation.StatusCode != HouseholdContributionStatuses.Corrected
                orderby obligation.DueDateUtc
                select new
                {
                    Obligation = obligation,
                    rule.ReminderDays
                })
                .ToArrayAsync(cancellationToken);

        foreach (var row in rows)
        {
            var obligation = row.Obligation;
            var outstandingMinor =
                Math.Max(
                    0,
                    obligation.AmountMinor -
                    obligation.PaidAmountMinor);

            var amount =
                HouseholdFinanceMoney.FromMinorUnits(
                    obligation.AmountMinor);

            var outstanding =
                HouseholdFinanceMoney.FromMinorUnits(
                    outstandingMinor);

            var isCurrentPeriod =
                obligation.DueDateUtc.Year == today.Year &&
                obligation.DueDateUtc.Month == today.Month;

            if (isCurrentPeriod ||
                obligation.CreatedAtUtc >= today.AddDays(-45))
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.ContributionObligationCreated",
                        NotificationCategoryCodes.Contribution,
                        NotificationSeverityCodes.Info,
                        $"Składka za {obligation.PeriodKey}",
                        $"Do rozliczenia jest składka {amount:N2} zł z terminem {obligation.DueDateUtc.ToLocalTime():dd.MM.yyyy}.",
                        true,
                        "/HouseholdFinance/Contributions",
                        "HouseholdContributionObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:contribution:{obligation.Id:D}:created"),
                    cancellationToken);
            }

            if (outstandingMinor <= 0 ||
                obligation.StatusCode == HouseholdContributionStatuses.Paid)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.ContributionPaid",
                        NotificationCategoryCodes.Contribution,
                        NotificationSeverityCodes.Info,
                        "Składka została opłacona",
                        $"Składka za {obligation.PeriodKey} jest rozliczona w całości.",
                        true,
                        "/HouseholdFinance/Contributions",
                        "HouseholdContributionObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:contribution:{obligation.Id:D}:paid"),
                    cancellationToken);

                continue;
            }

            if (obligation.PaidAmountMinor > 0)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.ContributionPartiallyPaid",
                        NotificationCategoryCodes.Contribution,
                        NotificationSeverityCodes.Info,
                        "Składka jest częściowo opłacona",
                        $"Za {obligation.PeriodKey} pozostało do wpłaty {outstanding:N2} zł.",
                        true,
                        "/HouseholdFinance/Contributions",
                        "HouseholdContributionObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:contribution:{obligation.Id:D}:partial:{obligation.PaidAmountMinor}"),
                    cancellationToken);
            }

            var daysToDue =
                (obligation.DueDateUtc.Date - today).Days;

            if (daysToDue < 0)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.ContributionOverdue",
                        NotificationCategoryCodes.Contribution,
                        NotificationSeverityCodes.Warning,
                        "Składka jest po terminie",
                        $"Składka za {obligation.PeriodKey} ma zaległość {outstanding:N2} zł. Termin minął {Math.Abs(daysToDue)} dni temu.",
                        true,
                        "/HouseholdFinance/Contributions",
                        "HouseholdContributionObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:contribution:{obligation.Id:D}:overdue"),
                    cancellationToken);
            }
            else if (daysToDue == 0)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.ContributionDueToday",
                        NotificationCategoryCodes.Contribution,
                        NotificationSeverityCodes.Warning,
                        "Termin składki jest dzisiaj",
                        $"Do wpłaty za {obligation.PeriodKey} pozostało {outstanding:N2} zł.",
                        true,
                        "/HouseholdFinance/Contributions",
                        "HouseholdContributionObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:contribution:{obligation.Id:D}:due-today"),
                    cancellationToken);
            }
            else if (daysToDue <= Math.Clamp(row.ReminderDays, 0, 31))
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.ContributionUpcoming",
                        NotificationCategoryCodes.Contribution,
                        NotificationSeverityCodes.Important,
                        "Zbliża się termin składki",
                        $"Składka za {obligation.PeriodKey}: {outstanding:N2} zł do {obligation.DueDateUtc.ToLocalTime():dd.MM.yyyy}.",
                        true,
                        "/HouseholdFinance/Contributions",
                        "HouseholdContributionObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:contribution:{obligation.Id:D}:upcoming"),
                    cancellationToken);
            }
        }
    }

    private async Task EnsureContributionPaymentNotificationsAsync(
        Guid userId,
        UserHouseholdContext context,
        CancellationToken cancellationToken)
    {
        var ownRequests =
            await dbContext.HouseholdContributionPaymentRequests
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == context.HouseholdId &&
                    x.SubmittedByUserId == userId &&
                    x.SubmittedAtUtc >= DateTime.UtcNow.AddMonths(-6))
                .OrderByDescending(x => x.SubmittedAtUtc)
                .Take(100)
                .ToArrayAsync(cancellationToken);

        foreach (var payment in ownRequests)
        {
            var amount =
                HouseholdFinanceMoney.FromMinorUnits(
                    payment.AmountMinor);

            var eventCode =
                payment.StatusCode switch
                {
                    HouseholdContributionPaymentStatuses.Approved =>
                        "M04.10.2.ContributionPaymentApproved",
                    HouseholdContributionPaymentStatuses.Rejected =>
                        "M04.10.2.ContributionPaymentRejected",
                    _ =>
                        "M04.10.2.ContributionPaymentSubmitted"
                };

            var severity =
                payment.StatusCode switch
                {
                    HouseholdContributionPaymentStatuses.Approved =>
                        NotificationSeverityCodes.Important,
                    HouseholdContributionPaymentStatuses.Rejected =>
                        NotificationSeverityCodes.Warning,
                    _ =>
                        NotificationSeverityCodes.Info
                };

            var title =
                payment.StatusCode switch
                {
                    HouseholdContributionPaymentStatuses.Approved =>
                        "Wpłata składki zaakceptowana",
                    HouseholdContributionPaymentStatuses.Rejected =>
                        "Wpłata składki odrzucona",
                    _ =>
                        "Wpłata składki wysłana"
                };

            var message =
                payment.StatusCode switch
                {
                    HouseholdContributionPaymentStatuses.Approved =>
                        $"Wpłata {amount:N2} zł została zaakceptowana i rozliczona.",
                    HouseholdContributionPaymentStatuses.Rejected =>
                        string.IsNullOrWhiteSpace(payment.ReviewNote)
                            ? $"Wpłata {amount:N2} zł została odrzucona."
                            : $"Wpłata {amount:N2} zł została odrzucona. Powód: {payment.ReviewNote}",
                    _ =>
                        $"Wpłata {amount:N2} zł oczekuje na akceptację."
                };

            await PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    eventCode,
                    NotificationCategoryCodes.Contribution,
                    severity,
                    title,
                    message,
                    true,
                    "/HouseholdFinance/Contributions",
                    "HouseholdContributionPaymentRequest",
                    payment.Id.ToString(),
                    $"M04.10.2:contribution-payment:{payment.Id:D}:{payment.StatusCode}"),
                cancellationToken);
        }

        var canApprove =
            await PermissionEnforcement.HasUserAsync(
                dbContext,
                userId,
                SystemPermissions.FinanceHouseholdApprove,
                cancellationToken);

        if (!canApprove)
        {
            return;
        }

        var pendingForApproval =
            await (
                from payment in dbContext.HouseholdContributionPaymentRequests
                    .AsNoTracking()
                join member in dbContext.HouseholdMembers
                    .AsNoTracking()
                    on payment.HouseholdMemberId equals member.Id
                join person in dbContext.People
                    .AsNoTracking()
                    on member.PersonId equals person.Id
                where
                    payment.HouseholdId == context.HouseholdId &&
                    payment.StatusCode == HouseholdContributionPaymentStatuses.Pending &&
                    payment.SubmittedByUserId != userId
                orderby payment.SubmittedAtUtc
                select new
                {
                    Payment = payment,
                    person.DisplayName,
                    person.FirstName,
                    person.LastName
                })
                .Take(100)
                .ToArrayAsync(cancellationToken);

        foreach (var row in pendingForApproval)
        {
            var amount =
                HouseholdFinanceMoney.FromMinorUnits(
                    row.Payment.AmountMinor);

            await PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    "M04.10.2.ContributionPaymentNeedsApproval",
                    NotificationCategoryCodes.Contribution,
                    NotificationSeverityCodes.Important,
                    "Wpłata składki czeka na akceptację",
                    $"{BuildDisplayName(row.DisplayName, row.FirstName, row.LastName)} wysłał(a) wpłatę {amount:N2} zł.",
                    true,
                    "/HouseholdFinance/Contributions",
                    "HouseholdContributionPaymentRequest",
                    row.Payment.Id.ToString(),
                    $"M04.10.2:contribution-payment:{row.Payment.Id:D}:approval-needed"),
                cancellationToken);
        }
    }

    private async Task EnsureMemberObligationNotificationsAsync(
        Guid userId,
        UserHouseholdContext context,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var rows =
            await dbContext.HouseholdMemberObligations
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == context.HouseholdId &&
                    x.HouseholdMemberId == context.HouseholdMemberId &&
                    x.DueDateUtc >= today.AddMonths(-6) &&
                    x.DueDateUtc < today.AddMonths(6))
                .OrderBy(x => x.DueDateUtc)
                .ToArrayAsync(cancellationToken);

        foreach (var obligation in rows)
        {
            var outstandingMinor =
                Math.Max(
                    0,
                    obligation.AmountMinor -
                    obligation.PaidAmountMinor);

            var amount =
                HouseholdFinanceMoney.FromMinorUnits(
                    obligation.AmountMinor);

            var outstanding =
                HouseholdFinanceMoney.FromMinorUnits(
                    outstandingMinor);

            var state =
                ResolveMemberObligationState(
                    obligation,
                    today);

            if (obligation.CreatedAtUtc >= today.AddDays(-45) ||
                (obligation.DueDateUtc.Date >= today &&
                 obligation.DueDateUtc.Date <= today.AddDays(31)))
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.MemberObligationCreated",
                        NotificationCategoryCodes.Household,
                        NotificationSeverityCodes.Info,
                        "Nowe zobowiązanie dla domu",
                        $"{obligation.Description}: {amount:N2} zł, termin {obligation.DueDateUtc.ToLocalTime():dd.MM.yyyy}.",
                        true,
                        "/HouseholdFinance/MemberObligations",
                        "HouseholdMemberObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:member-obligation:{obligation.Id:D}:created"),
                    cancellationToken);
            }

            if (state == HouseholdMemberObligationStatuses.Cancelled)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.MemberObligationCancelled",
                        NotificationCategoryCodes.Household,
                        NotificationSeverityCodes.Info,
                        "Zobowiązanie zostało anulowane",
                        $"Zobowiązanie „{obligation.Description}” nie wymaga już wpłaty.",
                        true,
                        "/HouseholdFinance/MemberObligations",
                        "HouseholdMemberObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:member-obligation:{obligation.Id:D}:cancelled"),
                    cancellationToken);

                continue;
            }

            if (state == HouseholdMemberObligationStatuses.Paid)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.MemberObligationPaid",
                        NotificationCategoryCodes.Household,
                        NotificationSeverityCodes.Info,
                        "Zobowiązanie zostało opłacone",
                        $"Zobowiązanie „{obligation.Description}” jest rozliczone w całości.",
                        true,
                        "/HouseholdFinance/MemberObligations",
                        "HouseholdMemberObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:member-obligation:{obligation.Id:D}:paid"),
                    cancellationToken);

                continue;
            }

            if (state == HouseholdMemberObligationStatuses.PartiallyPaid)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.MemberObligationPartiallyPaid",
                        NotificationCategoryCodes.Household,
                        NotificationSeverityCodes.Info,
                        "Zobowiązanie jest częściowo opłacone",
                        $"Dla „{obligation.Description}” pozostało {outstanding:N2} zł.",
                        true,
                        "/HouseholdFinance/MemberObligations",
                        "HouseholdMemberObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:member-obligation:{obligation.Id:D}:partial:{obligation.PaidAmountMinor}"),
                    cancellationToken);
            }

            var daysToDue =
                (obligation.DueDateUtc.Date - today).Days;

            if (daysToDue < 0)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.MemberObligationOverdue",
                        NotificationCategoryCodes.Household,
                        NotificationSeverityCodes.Warning,
                        "Zobowiązanie jest po terminie",
                        $"„{obligation.Description}” ma zaległość {outstanding:N2} zł.",
                        true,
                        "/HouseholdFinance/MemberObligations",
                        "HouseholdMemberObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:member-obligation:{obligation.Id:D}:overdue"),
                    cancellationToken);
            }
            else if (daysToDue == 0)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.MemberObligationDueToday",
                        NotificationCategoryCodes.Household,
                        NotificationSeverityCodes.Warning,
                        "Termin zobowiązania jest dzisiaj",
                        $"„{obligation.Description}”: pozostało {outstanding:N2} zł.",
                        true,
                        "/HouseholdFinance/MemberObligations",
                        "HouseholdMemberObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:member-obligation:{obligation.Id:D}:due-today"),
                    cancellationToken);
            }
            else if (daysToDue <= 7)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.2.MemberObligationUpcoming",
                        NotificationCategoryCodes.Household,
                        NotificationSeverityCodes.Important,
                        "Zbliża się termin zobowiązania",
                        $"„{obligation.Description}”: {outstanding:N2} zł do {obligation.DueDateUtc.ToLocalTime():dd.MM.yyyy}.",
                        true,
                        "/HouseholdFinance/MemberObligations",
                        "HouseholdMemberObligation",
                        obligation.Id.ToString(),
                        $"M04.10.2:member-obligation:{obligation.Id:D}:upcoming"),
                    cancellationToken);
            }
        }
    }

    private async Task EnsureMemberObligationPaymentNotificationsAsync(
        Guid userId,
        UserHouseholdContext context,
        CancellationToken cancellationToken)
    {
        var ownRequests =
            await dbContext.HouseholdMemberObligationPaymentRequests
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == context.HouseholdId &&
                    x.SubmittedByUserId == userId &&
                    x.SubmittedAtUtc >= DateTime.UtcNow.AddMonths(-6))
                .OrderByDescending(x => x.SubmittedAtUtc)
                .Take(100)
                .ToArrayAsync(cancellationToken);

        foreach (var payment in ownRequests)
        {
            var amount =
                HouseholdFinanceMoney.FromMinorUnits(
                    payment.AmountMinor);

            var eventCode =
                payment.StatusCode switch
                {
                    HouseholdMemberObligationPaymentStatuses.Approved =>
                        "M04.10.2.MemberObligationPaymentApproved",
                    HouseholdMemberObligationPaymentStatuses.Rejected =>
                        "M04.10.2.MemberObligationPaymentRejected",
                    _ =>
                        "M04.10.2.MemberObligationPaymentSubmitted"
                };

            var severity =
                payment.StatusCode switch
                {
                    HouseholdMemberObligationPaymentStatuses.Approved =>
                        NotificationSeverityCodes.Important,
                    HouseholdMemberObligationPaymentStatuses.Rejected =>
                        NotificationSeverityCodes.Warning,
                    _ =>
                        NotificationSeverityCodes.Info
                };

            var title =
                payment.StatusCode switch
                {
                    HouseholdMemberObligationPaymentStatuses.Approved =>
                        "Wpłata zobowiązania zaakceptowana",
                    HouseholdMemberObligationPaymentStatuses.Rejected =>
                        "Wpłata zobowiązania odrzucona",
                    _ =>
                        "Wpłata zobowiązania wysłana"
                };

            var message =
                payment.StatusCode switch
                {
                    HouseholdMemberObligationPaymentStatuses.Approved =>
                        $"Wpłata {amount:N2} zł została zaakceptowana i rozliczona.",
                    HouseholdMemberObligationPaymentStatuses.Rejected =>
                        string.IsNullOrWhiteSpace(payment.ReviewNote)
                            ? $"Wpłata {amount:N2} zł została odrzucona."
                            : $"Wpłata {amount:N2} zł została odrzucona. Powód: {payment.ReviewNote}",
                    _ =>
                        $"Wpłata {amount:N2} zł oczekuje na akceptację."
                };

            await PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    eventCode,
                    NotificationCategoryCodes.Household,
                    severity,
                    title,
                    message,
                    true,
                    "/HouseholdFinance/MemberObligations",
                    "HouseholdMemberObligationPaymentRequest",
                    payment.Id.ToString(),
                    $"M04.10.2:member-obligation-payment:{payment.Id:D}:{payment.StatusCode}"),
                cancellationToken);
        }

        var canApprove =
            await PermissionEnforcement.HasUserAsync(
                dbContext,
                userId,
                SystemPermissions.FinanceHouseholdApprove,
                cancellationToken);

        if (!canApprove)
        {
            return;
        }

        var pendingForApproval =
            await (
                from payment in dbContext.HouseholdMemberObligationPaymentRequests
                    .AsNoTracking()
                join member in dbContext.HouseholdMembers
                    .AsNoTracking()
                    on payment.HouseholdMemberId equals member.Id
                join person in dbContext.People
                    .AsNoTracking()
                    on member.PersonId equals person.Id
                where
                    payment.HouseholdId == context.HouseholdId &&
                    payment.StatusCode == HouseholdMemberObligationPaymentStatuses.Pending &&
                    payment.SubmittedByUserId != userId
                orderby payment.SubmittedAtUtc
                select new
                {
                    Payment = payment,
                    person.DisplayName,
                    person.FirstName,
                    person.LastName
                })
                .Take(100)
                .ToArrayAsync(cancellationToken);

        foreach (var row in pendingForApproval)
        {
            var amount =
                HouseholdFinanceMoney.FromMinorUnits(
                    row.Payment.AmountMinor);

            await PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    "M04.10.2.MemberObligationPaymentNeedsApproval",
                    NotificationCategoryCodes.Household,
                    NotificationSeverityCodes.Important,
                    "Wpłata zobowiązania czeka na akceptację",
                    $"{BuildDisplayName(row.DisplayName, row.FirstName, row.LastName)} wysłał(a) wpłatę {amount:N2} zł.",
                    true,
                    "/HouseholdFinance/MemberObligations",
                    "HouseholdMemberObligationPaymentRequest",
                    row.Payment.Id.ToString(),
                    $"M04.10.2:member-obligation-payment:{row.Payment.Id:D}:approval-needed"),
                cancellationToken);
        }
    }

    private async Task EnsureInvoiceNotificationsAsync(
        Guid userId,
        UserHouseholdContext context,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var recentEventCutoff = now.AddDays(-45);
        var recentPaymentCutoff = now.AddDays(-90);

        var invoices =
            await dbContext.HouseholdInvoices
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == context.HouseholdId)
                .OrderBy(x =>
                    x.StatusCode == HouseholdInvoiceStatuses.Paid ||
                    x.StatusCode == HouseholdInvoiceStatuses.Cancelled)
                .ThenBy(x => x.DueDateUtc)
                .ThenByDescending(x => x.CreatedAtUtc)
                .Take(250)
                .ToArrayAsync(cancellationToken);

        if (invoices.Length == 0)
        {
            return;
        }

        var invoiceIds =
            invoices
                .Select(x => x.Id)
                .ToArray();

        var payments =
            await dbContext.HouseholdInvoicePayments
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == context.HouseholdId &&
                    invoiceIds.Contains(x.InvoiceId))
                .OrderBy(x => x.PaidAtUtc)
                .ThenBy(x => x.CreatedAtUtc)
                .ToArrayAsync(cancellationToken);

        var paymentsByInvoice =
            payments
                .GroupBy(x => x.InvoiceId)
                .ToDictionary(
                    x => x.Key,
                    x => x.ToArray());

        foreach (var invoice in invoices)
        {
            paymentsByInvoice.TryGetValue(
                invoice.Id,
                out var invoicePayments);

            invoicePayments ??= [];

            var paidMinor =
                invoicePayments.Sum(x => x.AmountMinor);

            var remainingMinor =
                Math.Max(
                    0,
                    invoice.GrossAmountMinor - paidMinor);

            var grossAmount =
                HouseholdFinanceMoney.FromMinorUnits(
                    invoice.GrossAmountMinor);

            var paidAmount =
                HouseholdFinanceMoney.FromMinorUnits(
                    paidMinor);

            var remainingAmount =
                HouseholdFinanceMoney.FromMinorUnits(
                    remainingMinor);

            var invoiceLabel =
                string.IsNullOrWhiteSpace(invoice.InvoiceNumber)
                    ? invoice.Supplier
                    : $"{invoice.Supplier} · {invoice.InvoiceNumber}";

            if (invoice.CreatedAtUtc >= recentEventCutoff)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.3.InvoiceCreated",
                        NotificationCategoryCodes.Invoice,
                        NotificationSeverityCodes.Info,
                        "Dodano nową fakturę",
                        $"{invoiceLabel}: {grossAmount:N2} zł, termin {invoice.DueDateUtc.ToLocalTime():dd.MM.yyyy}.",
                        true,
                        "/HouseholdFinance/Invoices",
                        "HouseholdInvoice",
                        invoice.Id.ToString(),
                        $"M04.10.3:invoice:{invoice.Id:D}:created",
                        invoice.CreatedAtUtc),
                    cancellationToken);
            }

            foreach (var payment in invoicePayments
                .Where(x => x.CreatedAtUtc >= recentPaymentCutoff))
            {
                var paymentAmount =
                    HouseholdFinanceMoney.FromMinorUnits(
                        payment.AmountMinor);

                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.3.InvoicePaymentRegistered",
                        NotificationCategoryCodes.Invoice,
                        NotificationSeverityCodes.Info,
                        "Zarejestrowano płatność faktury",
                        $"{invoiceLabel}: zaksięgowano płatność {paymentAmount:N2} zł.",
                        true,
                        "/HouseholdFinance/Invoices",
                        "HouseholdInvoicePayment",
                        payment.Id.ToString(),
                        $"M04.10.3:invoice-payment:{payment.Id:D}:registered",
                        payment.CreatedAtUtc),
                    cancellationToken);
            }

            if (invoice.StatusCode == HouseholdInvoiceStatuses.Cancelled)
            {
                var cancelledAtUtc =
                    invoice.CancelledAtUtc ??
                    invoice.UpdatedAtUtc;

                if (cancelledAtUtc >= recentEventCutoff)
                {
                    await PublishAsync(
                        new PublishNotificationRequest(
                            [userId],
                            "M04.10.3.InvoiceCancelled",
                            NotificationCategoryCodes.Invoice,
                            NotificationSeverityCodes.Important,
                            "Faktura została anulowana",
                            $"{invoiceLabel} została anulowana.",
                            true,
                            "/HouseholdFinance/Invoices",
                            "HouseholdInvoice",
                            invoice.Id.ToString(),
                            $"M04.10.3:invoice:{invoice.Id:D}:cancelled",
                            cancelledAtUtc),
                        cancellationToken);
                }

                continue;
            }

            var effectiveStatus =
                paidMinor >= invoice.GrossAmountMinor
                    ? HouseholdInvoiceStatuses.Paid
                    : paidMinor > 0
                        ? HouseholdInvoiceStatuses.PartiallyPaid
                        : HouseholdInvoiceStatuses.Unpaid;

            var hasRecentPayment =
                invoicePayments.Any(x =>
                    x.CreatedAtUtc >= recentEventCutoff);

            if (effectiveStatus == HouseholdInvoiceStatuses.Paid)
            {
                if (invoice.UpdatedAtUtc >= recentEventCutoff ||
                    hasRecentPayment)
                {
                    await PublishAsync(
                        new PublishNotificationRequest(
                            [userId],
                            "M04.10.3.InvoicePaid",
                            NotificationCategoryCodes.Invoice,
                            NotificationSeverityCodes.Important,
                            "Faktura została opłacona",
                            $"{invoiceLabel} jest opłacona w całości ({grossAmount:N2} zł).",
                            true,
                            "/HouseholdFinance/Invoices",
                            "HouseholdInvoice",
                            invoice.Id.ToString(),
                            $"M04.10.3:invoice:{invoice.Id:D}:paid"),
                        cancellationToken);
                }

                continue;
            }

            if (effectiveStatus == HouseholdInvoiceStatuses.PartiallyPaid &&
                (invoice.UpdatedAtUtc >= recentEventCutoff || hasRecentPayment))
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.3.InvoicePartiallyPaid",
                        NotificationCategoryCodes.Invoice,
                        NotificationSeverityCodes.Info,
                        "Faktura jest częściowo opłacona",
                        $"{invoiceLabel}: wpłacono {paidAmount:N2} zł, pozostało {remainingAmount:N2} zł.",
                        true,
                        "/HouseholdFinance/Invoices",
                        "HouseholdInvoice",
                        invoice.Id.ToString(),
                        $"M04.10.3:invoice:{invoice.Id:D}:partial:{paidMinor}"),
                    cancellationToken);
            }

            if (remainingMinor <= 0)
            {
                continue;
            }

            var daysToDue =
                (invoice.DueDateUtc.Date - today).Days;

            if (daysToDue < 0)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.3.InvoiceOverdue",
                        NotificationCategoryCodes.Invoice,
                        NotificationSeverityCodes.Warning,
                        "Faktura jest po terminie",
                        $"{invoiceLabel}: pozostało {remainingAmount:N2} zł. Termin płatności minął {invoice.DueDateUtc.ToLocalTime():dd.MM.yyyy}.",
                        true,
                        "/HouseholdFinance/Invoices",
                        "HouseholdInvoice",
                        invoice.Id.ToString(),
                        $"M04.10.3:invoice:{invoice.Id:D}:overdue"),
                    cancellationToken);
            }
            else if (daysToDue == 0)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.3.InvoiceDueToday",
                        NotificationCategoryCodes.Invoice,
                        NotificationSeverityCodes.Warning,
                        "Termin faktury jest dzisiaj",
                        $"{invoiceLabel}: do zapłaty pozostało {remainingAmount:N2} zł.",
                        true,
                        "/HouseholdFinance/Invoices",
                        "HouseholdInvoice",
                        invoice.Id.ToString(),
                        $"M04.10.3:invoice:{invoice.Id:D}:due-today"),
                    cancellationToken);
            }
            else if (daysToDue <= 7)
            {
                await PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.3.InvoiceUpcoming",
                        NotificationCategoryCodes.Invoice,
                        NotificationSeverityCodes.Important,
                        "Zbliża się termin faktury",
                        $"{invoiceLabel}: {remainingAmount:N2} zł do {invoice.DueDateUtc.ToLocalTime():dd.MM.yyyy}.",
                        true,
                        "/HouseholdFinance/Invoices",
                        "HouseholdInvoice",
                        invoice.Id.ToString(),
                        $"M04.10.3:invoice:{invoice.Id:D}:upcoming"),
                    cancellationToken);
            }
        }
    }

    private async Task<UserHouseholdContext?> GetUserHouseholdContextAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await (
            from account in dbContext.UserAccounts
                .AsNoTracking()
            join person in dbContext.People
                .AsNoTracking()
                on account.PersonId equals person.Id
            join membership in dbContext.HouseholdMembers
                .AsNoTracking()
                on person.Id equals membership.PersonId
            where
                account.Id == userId &&
                account.IsActive &&
                person.IsActive &&
                membership.IsActive
            orderby membership.JoinedAtUtc descending
            select new UserHouseholdContext(
                person.Id,
                membership.Id,
                membership.HouseholdId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string ResolveMemberObligationState(
        HouseholdMemberObligation obligation,
        DateTime todayUtc)
    {
        if (obligation.StatusCode ==
            HouseholdMemberObligationStatuses.Cancelled)
        {
            return HouseholdMemberObligationStatuses.Cancelled;
        }

        if (obligation.PaidAmountMinor >= obligation.AmountMinor)
        {
            return HouseholdMemberObligationStatuses.Paid;
        }

        if (obligation.DueDateUtc.Date < todayUtc.Date)
        {
            return HouseholdMemberObligationStatuses.Overdue;
        }

        return obligation.PaidAmountMinor > 0
            ? HouseholdMemberObligationStatuses.PartiallyPaid
            : HouseholdMemberObligationStatuses.Pending;
    }

    private static string BuildDisplayName(
        string? displayName,
        string firstName,
        string lastName) =>
        string.IsNullOrWhiteSpace(displayName)
            ? $"{firstName} {lastName}".Trim()
            : displayName.Trim();

    private async Task<IReadOnlyList<NotificationItem>>
        ReadItemsAsync(
            string sql,
            IReadOnlyList<ParameterValue> parameters,
            CancellationToken cancellationToken)
    {
        var result =
            new List<NotificationItem>();

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

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    var categoryCode =
                        reader.GetString(2);
                    var severityCode =
                        reader.GetString(3);

                    result.Add(
                        new NotificationItem(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            categoryCode,
                            NotificationCategoryCodes.GetNamePl(
                                categoryCode),
                            severityCode,
                            NotificationSeverityCodes.GetNamePl(
                                severityCode),
                            reader.GetString(4),
                            reader.GetString(5),
                            reader.IsDBNull(6)
                                ? null
                                : reader.GetString(6),
                            reader.IsDBNull(7)
                                ? null
                                : reader.GetString(7),
                            reader.IsDBNull(8)
                                ? null
                                : reader.GetString(8),
                            reader.GetInt64(9) != 0,
                            reader.GetInt64(10) != 0,
                            reader.IsDBNull(11)
                                ? null
                                : ReadDateTime(
                                    reader,
                                    11),
                            ReadDateTime(
                                reader,
                                12)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<long> ScalarLongAsync(
        string sql,
        IReadOnlyList<ParameterValue> parameters,
        CancellationToken cancellationToken)
    {
        long result = 0;

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
                    Convert.ToInt64(
                        await command.ExecuteScalarAsync(
                            cancellationToken)
                        ?? 0);
            },
            cancellationToken);

        return result;
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
            await action(connection);
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

        parameter.ParameterName = name;
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

        var parsed =
            Convert.ToDateTime(
                value);

        return DateTime.SpecifyKind(
            parsed,
            DateTimeKind.Utc);
    }

    private static DateTime NormalizeUtc(
        DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local =>
                value.ToUniversalTime(),
            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };

    private static string NormalizeRequired(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{fieldName} nie może być pusty.");
        }

        var normalized =
            value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"{fieldName} może mieć maksymalnie {maxLength} znaków.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized =
            value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"Wartość może mieć maksymalnie {maxLength} znaków.");
        }

        return normalized;
    }

    private sealed record UserHouseholdContext(
        Guid PersonId,
        Guid HouseholdMemberId,
        Guid HouseholdId);

    private sealed record ParameterValue(
        string Name,
        object? Value);
}

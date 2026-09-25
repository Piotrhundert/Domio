namespace Domio.Application.Notifications;

public sealed record NotificationUserSettings(
    Guid UserId,
    bool InAppEnabled,
    bool EmailEnabled,
    string? EmailAddress,
    bool Reminder7Days,
    bool Reminder3Days,
    bool Reminder1Day,
    bool ReminderDueDay,
    bool ReminderOverdue,
    bool NotifySystem,
    bool NotifyPersonalFinance,
    bool NotifyHousehold,
    bool NotifyContributions,
    bool NotifyInvoices,
    bool NotifyFamily,
    bool NotifyGoals,
    bool NotifyUsers,
    DateTime? EmailEnabledAtUtc,
    DateTime UpdatedAtUtc)
{
    public static NotificationUserSettings CreateDefault(
        Guid userId) =>
        new(
            userId,
            InAppEnabled: true,
            EmailEnabled: false,
            EmailAddress: null,
            Reminder7Days: true,
            Reminder3Days: true,
            Reminder1Day: true,
            ReminderDueDay: true,
            ReminderOverdue: true,
            NotifySystem: true,
            NotifyPersonalFinance: true,
            NotifyHousehold: true,
            NotifyContributions: true,
            NotifyInvoices: true,
            NotifyFamily: true,
            NotifyGoals: true,
            NotifyUsers: true,
            EmailEnabledAtUtc: null,
            UpdatedAtUtc: DateTime.UtcNow);
}

public sealed record UpdateNotificationUserSettingsRequest(
    bool InAppEnabled,
    bool EmailEnabled,
    string? EmailAddress,
    bool Reminder7Days,
    bool Reminder3Days,
    bool Reminder1Day,
    bool ReminderDueDay,
    bool ReminderOverdue,
    bool NotifySystem,
    bool NotifyPersonalFinance,
    bool NotifyHousehold,
    bool NotifyContributions,
    bool NotifyInvoices,
    bool NotifyFamily,
    bool NotifyGoals,
    bool NotifyUsers);

public sealed record NotificationEmailConfiguration(
    bool IsConfigured,
    bool IsEnabled,
    string SenderName,
    string SenderEmail,
    string SmtpHost,
    int SmtpPort,
    string SmtpUsername,
    bool HasPassword,
    bool UseSsl,
    string? ApplicationBaseUrl,
    DateTime? UpdatedAtUtc);

public sealed record UpdateNotificationEmailConfigurationRequest(
    bool IsEnabled,
    string SenderName,
    string SenderEmail,
    string SmtpHost,
    int SmtpPort,
    string SmtpUsername,
    string? SmtpPassword,
    bool UseSsl,
    string? ApplicationBaseUrl);

public sealed record NotificationEmailDeliverySummary(
    Guid DeliveryId,
    Guid? NotificationId,
    string RecipientEmail,
    string Subject,
    string StatusCode,
    int AttemptCount,
    DateTime CreatedAtUtc,
    DateTime? LastAttemptAtUtc,
    DateTime? SentAtUtc,
    string? LastError,
    bool IsTest);

public static class NotificationEmailDeliveryStatuses
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";

    public static string GetNamePl(
        string code) =>
        code switch
        {
            Pending => "Oczekuje",
            Sent => "Wysłano",
            Failed => "Błąd",
            _ => code
        };
}

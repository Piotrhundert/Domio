using System.ComponentModel.DataAnnotations;
using Domio.Application.Notifications;

namespace Domio.Web.Models.Notifications;

public sealed class NotificationSettingsPageViewModel
{
    public required NotificationUserSettingsFormViewModel UserSettings
    {
        get;
        init;
    }

    public NotificationEmailConfigurationFormViewModel? EmailConfiguration
    {
        get;
        init;
    }

    public IReadOnlyList<NotificationEmailDeliverySummary>
        EmailHistory
    {
        get;
        init;
    } = [];

    public bool CanManageEmail
    {
        get;
        init;
    }

    public string? AccountEmail
    {
        get;
        init;
    }
}

public sealed class NotificationUserSettingsFormViewModel
{
    public bool InAppEnabled { get; set; }

    public bool EmailEnabled { get; set; }

    [EmailAddress]
    [StringLength(320)]
    public string? EmailAddress { get; set; }

    public bool Reminder7Days { get; set; }

    public bool Reminder3Days { get; set; }

    public bool Reminder1Day { get; set; }

    public bool ReminderDueDay { get; set; }

    public bool ReminderOverdue { get; set; }

    public bool NotifySystem { get; set; }

    public bool NotifyPersonalFinance { get; set; }

    public bool NotifyHousehold { get; set; }

    public bool NotifyContributions { get; set; }

    public bool NotifyInvoices { get; set; }

    public bool NotifyFamily { get; set; }

    public bool NotifyGoals { get; set; }

    public bool NotifyUsers { get; set; }
}

public sealed class NotificationEmailConfigurationFormViewModel
{
    public bool IsConfigured { get; set; }

    public bool IsEnabled { get; set; }

    [Required]
    [StringLength(160)]
    public string SenderName { get; set; } = "Domio";

    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string SenderEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    [StringLength(255)]
    public string SmtpUsername { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string? SmtpPassword { get; set; }

    public bool HasPassword { get; set; }

    public bool UseSsl { get; set; } = true;

    [Url]
    [StringLength(500)]
    public string? ApplicationBaseUrl { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    [EmailAddress]
    [StringLength(320)]
    public string? TestRecipientEmail { get; set; }
}

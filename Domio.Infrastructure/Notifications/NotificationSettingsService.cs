using System.Data;
using System.Data.Common;
using System.Net.Mail;
using Domio.Application.Notifications;
using Domio.Domain.Users;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Notifications;

public sealed class NotificationSettingsService(
    DomioDbContext dbContext,
    NotificationSecretProtector secretProtector)
    : INotificationSettingsService
{
    public async Task<NotificationUserSettings> GetUserSettingsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            userId,
            SystemPermissions.NotificationsView,
            cancellationToken);

        var result =
            await GetUserSettingsCoreAsync(
                userId,
                cancellationToken);

        return result ??
            NotificationUserSettings.CreateDefault(
                userId);
    }

    public async Task SaveUserSettingsAsync(
        Guid userId,
        UpdateNotificationUserSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            userId,
            SystemPermissions.NotificationsView,
            cancellationToken);

        var normalizedEmail =
            NormalizeOptionalEmail(
                request.EmailAddress);

        var existing =
            await GetUserSettingsCoreAsync(
                userId,
                cancellationToken);

        var now =
            DateTime.UtcNow;

        DateTime? emailEnabledAtUtc =
            request.EmailEnabled
                ? existing?.EmailEnabled == true &&
                  existing.EmailEnabledAtUtc.HasValue
                    ? existing.EmailEnabledAtUtc
                    : now
                : null;

        await ExecuteAsync(
            """
            INSERT INTO NotificationUserSettings
                (UserId, InAppEnabled, EmailEnabled, EmailAddress,
                 Reminder7Days, Reminder3Days, Reminder1Day,
                 ReminderDueDay, ReminderOverdue,
                 NotifySystem, NotifyPersonalFinance, NotifyHousehold,
                 NotifyContributions, NotifyInvoices, NotifyFamily,
                 NotifyGoals, NotifyUsers,
                 EmailEnabledAtUtc, UpdatedAtUtc)
            VALUES
                ($userId, $inAppEnabled, $emailEnabled, $emailAddress,
                 $reminder7Days, $reminder3Days, $reminder1Day,
                 $reminderDueDay, $reminderOverdue,
                 $notifySystem, $notifyPersonalFinance, $notifyHousehold,
                 $notifyContributions, $notifyInvoices, $notifyFamily,
                 $notifyGoals, $notifyUsers,
                 $emailEnabledAtUtc, $updatedAtUtc)
            ON CONFLICT(UserId) DO UPDATE SET
                InAppEnabled = excluded.InAppEnabled,
                EmailEnabled = excluded.EmailEnabled,
                EmailAddress = excluded.EmailAddress,
                Reminder7Days = excluded.Reminder7Days,
                Reminder3Days = excluded.Reminder3Days,
                Reminder1Day = excluded.Reminder1Day,
                ReminderDueDay = excluded.ReminderDueDay,
                ReminderOverdue = excluded.ReminderOverdue,
                NotifySystem = excluded.NotifySystem,
                NotifyPersonalFinance = excluded.NotifyPersonalFinance,
                NotifyHousehold = excluded.NotifyHousehold,
                NotifyContributions = excluded.NotifyContributions,
                NotifyInvoices = excluded.NotifyInvoices,
                NotifyFamily = excluded.NotifyFamily,
                NotifyGoals = excluded.NotifyGoals,
                NotifyUsers = excluded.NotifyUsers,
                EmailEnabledAtUtc = excluded.EmailEnabledAtUtc,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """,
            [
                P("$userId", userId),
                P("$inAppEnabled", request.InAppEnabled ? 1 : 0),
                P("$emailEnabled", request.EmailEnabled ? 1 : 0),
                P("$emailAddress", normalizedEmail),
                P("$reminder7Days", request.Reminder7Days ? 1 : 0),
                P("$reminder3Days", request.Reminder3Days ? 1 : 0),
                P("$reminder1Day", request.Reminder1Day ? 1 : 0),
                P("$reminderDueDay", request.ReminderDueDay ? 1 : 0),
                P("$reminderOverdue", request.ReminderOverdue ? 1 : 0),
                P("$notifySystem", request.NotifySystem ? 1 : 0),
                P("$notifyPersonalFinance", request.NotifyPersonalFinance ? 1 : 0),
                P("$notifyHousehold", request.NotifyHousehold ? 1 : 0),
                P("$notifyContributions", request.NotifyContributions ? 1 : 0),
                P("$notifyInvoices", request.NotifyInvoices ? 1 : 0),
                P("$notifyFamily", request.NotifyFamily ? 1 : 0),
                P("$notifyGoals", request.NotifyGoals ? 1 : 0),
                P("$notifyUsers", request.NotifyUsers ? 1 : 0),
                P("$emailEnabledAtUtc", emailEnabledAtUtc),
                P("$updatedAtUtc", now)
            ],
            cancellationToken);
    }

    public async Task<NotificationEmailConfiguration> GetEmailConfigurationAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.NotificationsManage,
            cancellationToken);

        return await GetEmailConfigurationCoreAsync(
            cancellationToken);
    }

    public async Task SaveEmailConfigurationAsync(
        Guid actorUserId,
        UpdateNotificationEmailConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.NotificationsManage,
            cancellationToken);

        var senderName =
            NormalizeRequired(
                request.SenderName,
                "Nazwa nadawcy",
                160);

        var senderEmail =
            NormalizeRequiredEmail(
                request.SenderEmail,
                "Adres nadawcy");

        var smtpHost =
            NormalizeRequired(
                request.SmtpHost,
                "Serwer SMTP",
                255);

        if (request.SmtpPort < 1 ||
            request.SmtpPort > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.SmtpPort),
                "Port SMTP musi mieścić się w zakresie 1-65535.");
        }

        var smtpUsername =
            string.IsNullOrWhiteSpace(
                request.SmtpUsername)
                ? string.Empty
                : NormalizeRequired(
                    request.SmtpUsername,
                    "Nazwa użytkownika SMTP",
                    255);

        var baseUrl =
            NormalizeApplicationBaseUrl(
                request.ApplicationBaseUrl);

        string? existingProtectedPassword =
            null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT ProtectedPassword
                    FROM NotificationEmailSettings
                    WHERE Id = 1;
                    """;

                var value =
                    await command.ExecuteScalarAsync(
                        cancellationToken);

                if (value is not null &&
                    value != DBNull.Value)
                {
                    existingProtectedPassword =
                        Convert.ToString(
                            value);
                }
            },
            cancellationToken);

        var protectedPassword =
            string.IsNullOrWhiteSpace(
                request.SmtpPassword)
                ? existingProtectedPassword
                : secretProtector.Protect(
                    request.SmtpPassword.Trim());

        if (request.IsEnabled &&
            string.IsNullOrWhiteSpace(
                protectedPassword) &&
            !string.IsNullOrWhiteSpace(
                smtpUsername))
        {
            throw new InvalidOperationException(
                "Dla konta SMTP z nazwą użytkownika podaj hasło.");
        }

        var now =
            DateTime.UtcNow;

        await ExecuteAsync(
            """
            INSERT INTO NotificationEmailSettings
                (Id, IsEnabled, SenderName, SenderEmail,
                 SmtpHost, SmtpPort, SmtpUsername, ProtectedPassword,
                 UseSsl, ApplicationBaseUrl,
                 UpdatedByUserId, UpdatedAtUtc)
            VALUES
                (1, $isEnabled, $senderName, $senderEmail,
                 $smtpHost, $smtpPort, $smtpUsername, $protectedPassword,
                 $useSsl, $applicationBaseUrl,
                 $updatedByUserId, $updatedAtUtc)
            ON CONFLICT(Id) DO UPDATE SET
                IsEnabled = excluded.IsEnabled,
                SenderName = excluded.SenderName,
                SenderEmail = excluded.SenderEmail,
                SmtpHost = excluded.SmtpHost,
                SmtpPort = excluded.SmtpPort,
                SmtpUsername = excluded.SmtpUsername,
                ProtectedPassword = excluded.ProtectedPassword,
                UseSsl = excluded.UseSsl,
                ApplicationBaseUrl = excluded.ApplicationBaseUrl,
                UpdatedByUserId = excluded.UpdatedByUserId,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """,
            [
                P("$isEnabled", request.IsEnabled ? 1 : 0),
                P("$senderName", senderName),
                P("$senderEmail", senderEmail),
                P("$smtpHost", smtpHost),
                P("$smtpPort", request.SmtpPort),
                P("$smtpUsername", smtpUsername),
                P("$protectedPassword", protectedPassword),
                P("$useSsl", request.UseSsl ? 1 : 0),
                P("$applicationBaseUrl", baseUrl),
                P("$updatedByUserId", actorUserId),
                P("$updatedAtUtc", now)
            ],
            cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationEmailDeliverySummary>>
        GetRecentEmailDeliveriesAsync(
            Guid actorUserId,
            int take = 30,
            CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.NotificationsManage,
            cancellationToken);

        take =
            Math.Clamp(
                take,
                1,
                100);

        var result =
            new List<NotificationEmailDeliverySummary>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT Id, NotificationId, RecipientEmail, Subject,
                           StatusCode, AttemptCount, CreatedAtUtc,
                           LastAttemptAtUtc, SentAtUtc, LastError, IsTest
                    FROM NotificationEmailDeliveries
                    ORDER BY CreatedAtUtc DESC
                    LIMIT $take;
                    """;

                AddParameter(
                    command,
                    "$take",
                    take);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new NotificationEmailDeliverySummary(
                            reader.GetGuid(0),
                            reader.IsDBNull(1)
                                ? null
                                : reader.GetGuid(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetString(4),
                            reader.GetInt32(5),
                            ReadDateTime(
                                reader,
                                6),
                            reader.IsDBNull(7)
                                ? null
                                : ReadDateTime(
                                    reader,
                                    7),
                            reader.IsDBNull(8)
                                ? null
                                : ReadDateTime(
                                    reader,
                                    8),
                            reader.IsDBNull(9)
                                ? null
                                : reader.GetString(9),
                            ReadBoolean(
                                reader,
                                10)));
                }
            },
            cancellationToken);

        return result;
    }

    internal async Task<NotificationUserSettings>
        GetUserSettingsForDeliveryAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
        await GetUserSettingsCoreAsync(
            userId,
            cancellationToken)
        ?? NotificationUserSettings.CreateDefault(
            userId);

    internal async Task<EmailConfigurationInternal>
        GetEmailConfigurationForDeliveryAsync(
            CancellationToken cancellationToken = default)
    {
        EmailConfigurationInternal? result =
            null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT IsEnabled, SenderName, SenderEmail,
                           SmtpHost, SmtpPort, SmtpUsername,
                           ProtectedPassword, UseSsl, ApplicationBaseUrl
                    FROM NotificationEmailSettings
                    WHERE Id = 1;
                    """;

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                if (await reader.ReadAsync(
                        cancellationToken))
                {
                    var protectedPassword =
                        reader.IsDBNull(6)
                            ? null
                            : reader.GetString(6);

                    result =
                        new EmailConfigurationInternal(
                            ReadBoolean(reader, 0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetInt32(4),
                            reader.GetString(5),
                            string.IsNullOrWhiteSpace(
                                protectedPassword)
                                ? string.Empty
                                : secretProtector.Unprotect(
                                    protectedPassword),
                            ReadBoolean(reader, 7),
                            reader.IsDBNull(8)
                                ? null
                                : reader.GetString(8));
                }
            },
            cancellationToken);

        return result ??
            new EmailConfigurationInternal(
                false,
                "Domio",
                string.Empty,
                string.Empty,
                587,
                string.Empty,
                string.Empty,
                true,
                null);
    }

    private async Task<NotificationUserSettings?>
        GetUserSettingsCoreAsync(
            Guid userId,
            CancellationToken cancellationToken)
    {
        NotificationUserSettings? result =
            null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT UserId, InAppEnabled, EmailEnabled, EmailAddress,
                           Reminder7Days, Reminder3Days, Reminder1Day,
                           ReminderDueDay, ReminderOverdue,
                           NotifySystem, NotifyPersonalFinance, NotifyHousehold,
                           NotifyContributions, NotifyInvoices, NotifyFamily,
                           NotifyGoals, NotifyUsers,
                           EmailEnabledAtUtc, UpdatedAtUtc
                    FROM NotificationUserSettings
                    WHERE UserId = $userId;
                    """;

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
                    result =
                        new NotificationUserSettings(
                            reader.GetGuid(0),
                            ReadBoolean(reader, 1),
                            ReadBoolean(reader, 2),
                            reader.IsDBNull(3)
                                ? null
                                : reader.GetString(3),
                            ReadBoolean(reader, 4),
                            ReadBoolean(reader, 5),
                            ReadBoolean(reader, 6),
                            ReadBoolean(reader, 7),
                            ReadBoolean(reader, 8),
                            ReadBoolean(reader, 9),
                            ReadBoolean(reader, 10),
                            ReadBoolean(reader, 11),
                            ReadBoolean(reader, 12),
                            ReadBoolean(reader, 13),
                            ReadBoolean(reader, 14),
                            ReadBoolean(reader, 15),
                            ReadBoolean(reader, 16),
                            reader.IsDBNull(17)
                                ? null
                                : ReadDateTime(reader, 17),
                            ReadDateTime(reader, 18));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<NotificationEmailConfiguration>
        GetEmailConfigurationCoreAsync(
            CancellationToken cancellationToken)
    {
        NotificationEmailConfiguration? result =
            null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT IsEnabled, SenderName, SenderEmail,
                           SmtpHost, SmtpPort, SmtpUsername,
                           ProtectedPassword, UseSsl,
                           ApplicationBaseUrl, UpdatedAtUtc
                    FROM NotificationEmailSettings
                    WHERE Id = 1;
                    """;

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                if (await reader.ReadAsync(
                        cancellationToken))
                {
                    var hasPassword =
                        !reader.IsDBNull(6) &&
                        !string.IsNullOrWhiteSpace(
                            reader.GetString(6));

                    result =
                        new NotificationEmailConfiguration(
                            IsConfigured:
                                !string.IsNullOrWhiteSpace(
                                    reader.GetString(2)) &&
                                !string.IsNullOrWhiteSpace(
                                    reader.GetString(3)),
                            IsEnabled:
                                ReadBoolean(reader, 0),
                            SenderName:
                                reader.GetString(1),
                            SenderEmail:
                                reader.GetString(2),
                            SmtpHost:
                                reader.GetString(3),
                            SmtpPort:
                                reader.GetInt32(4),
                            SmtpUsername:
                                reader.GetString(5),
                            HasPassword:
                                hasPassword,
                            UseSsl:
                                ReadBoolean(reader, 7),
                            ApplicationBaseUrl:
                                reader.IsDBNull(8)
                                    ? null
                                    : reader.GetString(8),
                            UpdatedAtUtc:
                                ReadDateTime(
                                    reader,
                                    9));
                }
            },
            cancellationToken);

        return result ??
            new NotificationEmailConfiguration(
                IsConfigured: false,
                IsEnabled: false,
                SenderName: "Domio",
                SenderEmail: string.Empty,
                SmtpHost: string.Empty,
                SmtpPort: 587,
                SmtpUsername: string.Empty,
                HasPassword: false,
                UseSsl: true,
                ApplicationBaseUrl: null,
                UpdatedAtUtc: null);
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

                command.CommandText =
                    sql;

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

    private static bool ReadBoolean(
        DbDataReader reader,
        int ordinal)
    {
        var value =
            reader.GetValue(
                ordinal);

        return value switch
        {
            bool boolean => boolean,
            long integer => integer != 0,
            int integer => integer != 0,
            _ => Convert.ToBoolean(value)
        };
    }

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

    private static string NormalizeRequired(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                $"{fieldName} nie może być puste.");
        }

        var normalized =
            value.Trim();

        if (normalized.Length >
            maxLength)
        {
            throw new ArgumentException(
                $"{fieldName} może mieć maksymalnie {maxLength} znaków.");
        }

        return normalized;
    }

    private static string NormalizeRequiredEmail(
        string? value,
        string fieldName)
    {
        var normalized =
            NormalizeRequired(
                value,
                fieldName,
                320);

        try
        {
            return new MailAddress(
                normalized)
                .Address;
        }
        catch (FormatException)
        {
            throw new ArgumentException(
                $"{fieldName} ma nieprawidłowy format.");
        }
    }

    private static string? NormalizeOptionalEmail(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        return NormalizeRequiredEmail(
            value,
            "Adres e-mail");
    }

    private static string? NormalizeApplicationBaseUrl(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var normalized =
            value.Trim()
                .TrimEnd('/');

        if (!Uri.TryCreate(
                normalized,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Adres aplikacji musi być pełnym adresem http:// lub https://.");
        }

        return normalized;
    }

    private sealed record ParameterValue(
        string Name,
        object? Value);

    internal sealed record EmailConfigurationInternal(
        bool IsEnabled,
        string SenderName,
        string SenderEmail,
        string SmtpHost,
        int SmtpPort,
        string SmtpUsername,
        string SmtpPassword,
        bool UseSsl,
        string? ApplicationBaseUrl);
}

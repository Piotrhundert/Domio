using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Mail;
using Domio.Application.Notifications;
using Domio.Domain.Users;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Notifications;

public sealed class NotificationEmailDispatcher(
    DomioDbContext dbContext,
    NotificationSettingsService settingsService)
    : INotificationEmailDispatcher
{
    public async Task SendTestAsync(
        Guid actorUserId,
        string recipientEmail,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.NotificationsManage,
            cancellationToken);

        var recipient =
            NormalizeEmail(
                recipientEmail);

        var configuration =
            await settingsService
                .GetEmailConfigurationForDeliveryAsync(
                    cancellationToken);

        EnsureConfigurationReady(
            configuration);

        var deliveryId =
            Guid.NewGuid();

        var subject =
            "Domio · wiadomość testowa";

        var body =
            "To jest wiadomość testowa konfiguracji e-mail Domio." +
            Environment.NewLine +
            Environment.NewLine +
            "Jeżeli ją widzisz, konfiguracja SMTP działa poprawnie.";

        await InsertDeliveryAsync(
            deliveryId,
            notificationId: null,
            recipientUserId: actorUserId,
            recipient,
            subject,
            body,
            isTest: true,
            cancellationToken);

        await SendDeliveryAsync(
            new PendingDelivery(
                deliveryId,
                recipient,
                subject,
                body,
                0),
            configuration,
            cancellationToken,
            throwOnError: true);
    }

    public async Task<int> DispatchPendingAsync(
        int maxBatch = 50,
        CancellationToken cancellationToken = default)
    {
        maxBatch =
            Math.Clamp(
                maxBatch,
                1,
                200);

        var configuration =
            await settingsService
                .GetEmailConfigurationForDeliveryAsync(
                    cancellationToken);

        if (!configuration.IsEnabled ||
            string.IsNullOrWhiteSpace(
                configuration.SenderEmail) ||
            string.IsNullOrWhiteSpace(
                configuration.SmtpHost))
        {
            return 0;
        }

        var pending =
            await ReadPendingAsync(
                maxBatch,
                cancellationToken);

        var sent = 0;

        foreach (var delivery in pending)
        {
            if (await SendDeliveryAsync(
                    delivery,
                    configuration,
                    cancellationToken,
                    throwOnError: false))
            {
                sent++;
            }
        }

        return sent;
    }

    private async Task<bool> SendDeliveryAsync(
        PendingDelivery delivery,
        NotificationSettingsService.EmailConfigurationInternal configuration,
        CancellationToken cancellationToken,
        bool throwOnError)
    {
        var now =
            DateTime.UtcNow;

        try
        {
            using var message =
                new MailMessage
                {
                    From =
                        new MailAddress(
                            configuration.SenderEmail,
                            configuration.SenderName),
                    Subject =
                        delivery.Subject,
                    Body =
                        delivery.Body,
                    IsBodyHtml =
                        false
                };

            message.To.Add(
                new MailAddress(
                    delivery.RecipientEmail));

            using var client =
                new SmtpClient(
                    configuration.SmtpHost,
                    configuration.SmtpPort)
                {
                    EnableSsl =
                        configuration.UseSsl,
                    DeliveryMethod =
                        SmtpDeliveryMethod.Network,
                    UseDefaultCredentials =
                        string.IsNullOrWhiteSpace(
                            configuration.SmtpUsername)
                };

            if (!string.IsNullOrWhiteSpace(
                    configuration.SmtpUsername))
            {
                client.Credentials =
                    new NetworkCredential(
                        configuration.SmtpUsername,
                        configuration.SmtpPassword);
            }

            await client
                .SendMailAsync(
                    message)
                .WaitAsync(
                    cancellationToken);

            await ExecuteAsync(
                """
                UPDATE NotificationEmailDeliveries
                SET StatusCode = 'Sent',
                    AttemptCount = AttemptCount + 1,
                    LastAttemptAtUtc = $now,
                    SentAtUtc = $now,
                    LastError = NULL
                WHERE Id = $id;
                """,
                [
                    P("$now", now),
                    P("$id", delivery.DeliveryId)
                ],
                cancellationToken);

            return true;
        }
        catch (Exception exception)
            when (exception is SmtpException or
                  FormatException or
                  InvalidOperationException or
                  TimeoutException)
        {
            var diagnosticParts =
                new List<string>();

            Exception? currentException =
                exception;

            while (currentException is not null &&
                   diagnosticParts.Count < 5)
            {
                if (!string.IsNullOrWhiteSpace(
                        currentException.Message) &&
                    !diagnosticParts.Contains(
                        currentException.Message,
                        StringComparer.Ordinal))
                {
                    diagnosticParts.Add(
                        currentException.Message.Trim());
                }

                currentException =
                    currentException.InnerException;
            }

            var diagnosticMessage =
                string.Join(
                    " | ",
                    diagnosticParts);

            var safeError =
                diagnosticMessage.Length > 1000
                    ? diagnosticMessage[..1000]
                    : diagnosticMessage;

            await ExecuteAsync(
                """
                UPDATE NotificationEmailDeliveries
                SET StatusCode = 'Failed',
                    AttemptCount = AttemptCount + 1,
                    LastAttemptAtUtc = $now,
                    LastError = $error
                WHERE Id = $id;
                """,
                [
                    P("$now", now),
                    P("$error", safeError),
                    P("$id", delivery.DeliveryId)
                ],
                cancellationToken);

            if (throwOnError)
            {
                throw new InvalidOperationException(
                    "Wysłanie wiadomości testowej nie powiodło się: " +
                    safeError,
                    exception);
            }

            return false;
        }
    }

    private async Task<IReadOnlyList<PendingDelivery>>
        ReadPendingAsync(
            int maxBatch,
            CancellationToken cancellationToken)
    {
        var result =
            new List<PendingDelivery>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT Id, RecipientEmail, Subject, Body, AttemptCount
                    FROM NotificationEmailDeliveries
                    WHERE StatusCode IN ('Pending', 'Failed')
                      AND AttemptCount < 3
                    ORDER BY CreatedAtUtc
                    LIMIT $take;
                    """;

                AddParameter(
                    command,
                    "$take",
                    maxBatch);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new PendingDelivery(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetInt32(4)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task InsertDeliveryAsync(
        Guid deliveryId,
        Guid? notificationId,
        Guid? recipientUserId,
        string recipientEmail,
        string subject,
        string body,
        bool isTest,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            """
            INSERT INTO NotificationEmailDeliveries
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
                 NULL, NULL, $isTest);
            """,
            [
                P("$id", deliveryId),
                P("$notificationId", notificationId),
                P("$recipientUserId", recipientUserId),
                P("$recipientEmail", recipientEmail),
                P("$subject", subject),
                P("$body", body),
                P("$createdAtUtc", DateTime.UtcNow),
                P("$isTest", isTest ? 1 : 0)
            ],
            cancellationToken);
    }

    private static void EnsureConfigurationReady(
        NotificationSettingsService.EmailConfigurationInternal configuration)
    {
        if (!configuration.IsEnabled)
        {
            throw new InvalidOperationException(
                "Kanał e-mail jest wyłączony.");
        }

        if (string.IsNullOrWhiteSpace(
                configuration.SenderEmail) ||
            string.IsNullOrWhiteSpace(
                configuration.SmtpHost))
        {
            throw new InvalidOperationException(
                "Konfiguracja SMTP jest niepełna.");
        }
    }

    private static string NormalizeEmail(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Podaj adres e-mail odbiorcy.");
        }

        try
        {
            return new MailAddress(
                value.Trim())
                .Address;
        }
        catch (FormatException)
        {
            throw new ArgumentException(
                "Adres e-mail odbiorcy ma nieprawidłowy format.");
        }
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

    private sealed record PendingDelivery(
        Guid DeliveryId,
        string RecipientEmail,
        string Subject,
        string Body,
        int AttemptCount);

    private sealed record ParameterValue(
        string Name,
        object? Value);
}

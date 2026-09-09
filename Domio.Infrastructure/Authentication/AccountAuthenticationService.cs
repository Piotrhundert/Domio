using System.Security.Cryptography;
using Domio.Application.Auditing;
using Domio.Application.Authentication;
using Domio.Domain.Users;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Authentication;

public sealed class AccountAuthenticationService(
    DomioDbContext dbContext,
    IAuditService auditService) : IAccountAuthenticationService
{
    private const int MaximumFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration =
        TimeSpan.FromMinutes(15);

    private const int PasswordIterations = 210_000;
    private const int SaltSize = 16;
    private const int PasswordHashSize = 32;
    private const string PasswordAlgorithm = "PBKDF2-SHA256";

    public Task<bool> HasAnyUserAsync(
        CancellationToken cancellationToken = default) =>
        dbContext.UserAccounts.AnyAsync(cancellationToken);

    public async Task<FirstAdministratorSetupResult>
        InitializeFirstAdministratorAsync(
            FirstAdministratorSetupRequest request,
            string correlationId,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await HasAnyUserAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Pierwsze konto administratora zostało już utworzone.");
        }

        ValidateRequired(request.FirstName, nameof(request.FirstName));
        ValidateRequired(request.LastName, nameof(request.LastName));
        ValidateRequired(request.LoginName, nameof(request.LoginName));
        ValidatePassword(request.Password);

        var now = DateTime.UtcNow;
        var loginName = request.LoginName.Trim();
        var normalizedLoginName = NormalizeLogin(loginName);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        if (await dbContext.UserAccounts.AnyAsync(
                x => x.NormalizedLoginName == normalizedLoginName,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Konto z takim loginem już istnieje.");
        }

        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DisplayName =
                $"{request.FirstName.Trim()} {request.LastName.Trim()}",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            RoleDefinitionId = SystemRoles.AdministratorId,
            LoginName = loginName,
            NormalizedLoginName = normalizedLoginName,
            PasswordHash = HashPassword(request.Password),
            PasswordChangedAtUtc = now,
            IsActive = true,
            FailedLoginAttempts = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.People.Add(person);
        dbContext.UserAccounts.Add(user);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M02.2.InitialAdministratorCreated",
                EntityType: "UserAccount",
                EntityId: user.Id.ToString(),
                ActorId: user.Id.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Utworzono pierwsze konto administratora Domio."),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new FirstAdministratorSetupResult(
            user.Id,
            person.Id,
            loginName);
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        string loginName,
        string password,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedLogin = NormalizeLogin(loginName);

        var account = await dbContext.UserAccounts
            .SingleOrDefaultAsync(
                x => x.NormalizedLoginName == normalizedLogin,
                cancellationToken);

        if (account is null)
        {
            await WriteLoginAuditAsync(
                eventType: "M02.2.LoginFailed",
                entityId: null,
                correlationId,
                "Nieudana próba logowania.",
                cancellationToken);

            return new AuthenticationResult(
                AuthenticationStatus.InvalidCredentials);
        }

        var now = DateTime.UtcNow;

        if (!account.IsActive)
        {
            await WriteLoginAuditAsync(
                "M02.2.LoginFailed",
                account.Id.ToString(),
                correlationId,
                "Próba logowania do nieaktywnego konta.",
                cancellationToken);

            return new AuthenticationResult(
                AuthenticationStatus.Inactive);
        }

        if (account.LockoutEndUtc is not null &&
            account.LockoutEndUtc > now)
        {
            await WriteLoginAuditAsync(
                "M02.2.LoginLocked",
                account.Id.ToString(),
                correlationId,
                "Próba logowania do czasowo zablokowanego konta.",
                cancellationToken);

            return new AuthenticationResult(
                AuthenticationStatus.Locked,
                LockoutEndUtc: account.LockoutEndUtc);
        }

        var passwordValid =
            account.PasswordHash is not null &&
            VerifyPassword(password, account.PasswordHash);

        if (!passwordValid)
        {
            account.FailedLoginAttempts++;

            if (account.FailedLoginAttempts >= MaximumFailedAttempts)
            {
                account.LockoutEndUtc = now.Add(LockoutDuration);
            }

            account.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            var locked = account.LockoutEndUtc is not null;

            await WriteLoginAuditAsync(
                locked
                    ? "M02.2.LoginLocked"
                    : "M02.2.LoginFailed",
                account.Id.ToString(),
                correlationId,
                locked
                    ? "Konto zostało czasowo zablokowane po kolejnych nieudanych próbach logowania."
                    : "Nieudana próba logowania.",
                cancellationToken);

            return locked
                ? new AuthenticationResult(
                    AuthenticationStatus.Locked,
                    LockoutEndUtc: account.LockoutEndUtc)
                : new AuthenticationResult(
                    AuthenticationStatus.InvalidCredentials);
        }

        var person = await dbContext.People
            .SingleAsync(
                x => x.Id == account.PersonId,
                cancellationToken);

        if (!person.IsActive)
        {
            await WriteLoginAuditAsync(
                "M02.2.LoginFailed",
                account.Id.ToString(),
                correlationId,
                "Próba logowania dla nieaktywnej osoby.",
                cancellationToken);

            return new AuthenticationResult(
                AuthenticationStatus.Inactive);
        }

        var role = await dbContext.RoleDefinitions
            .SingleAsync(
                x => x.Id == account.RoleDefinitionId,
                cancellationToken);

        account.FailedLoginAttempts = 0;
        account.LockoutEndUtc = null;
        account.LastLoginAtUtc = now;
        account.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteLoginAuditAsync(
            "M02.2.LoginSucceeded",
            account.Id.ToString(),
            correlationId,
            "Poprawne logowanie użytkownika.",
            cancellationToken);

        var displayName =
            string.IsNullOrWhiteSpace(person.DisplayName)
                ? $"{person.FirstName} {person.LastName}".Trim()
                : person.DisplayName.Trim();

        return new AuthenticationResult(
            AuthenticationStatus.Success,
            new AuthenticatedUser(
                account.Id,
                person.Id,
                account.LoginName,
                displayName,
                role.Code,
                role.NamePl));
    }

    public async Task RecordLogoutAsync(
        Guid userId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M02.2.Logout",
                EntityType: "UserAccount",
                EntityId: userId.ToString(),
                ActorId: userId.ToString(),
                CorrelationId: correlationId,
                Description: "Wylogowanie użytkownika."),
            cancellationToken);
    }

    private Task<Guid> WriteLoginAuditAsync(
        string eventType,
        string? entityId,
        string correlationId,
        string description,
        CancellationToken cancellationToken) =>
        auditService.WriteAsync(
            new AuditEntry(
                EventType: eventType,
                EntityType: "UserAccount",
                EntityId: entityId,
                ActorId: entityId,
                CorrelationId: correlationId,
                Description: description),
            cancellationToken);

    private static string NormalizeLogin(string loginName) =>
        (loginName ?? string.Empty)
            .Trim()
            .ToUpperInvariant();

    private static void ValidateRequired(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Wartość nie może być pusta.",
                parameterName);
        }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            password.Length < 10 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit))
        {
            throw new ArgumentException(
                "Hasło musi mieć co najmniej 10 znaków oraz zawierać małą literę, wielką literę i cyfrę.",
                nameof(password));
        }
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            PasswordHashSize);

        return string.Join(
            '$',
            PasswordAlgorithm,
            PasswordIterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    private static bool VerifyPassword(
        string password,
        string storedHash)
    {
        try
        {
            var parts = storedHash.Split('$');

            if (parts.Length != 4 ||
                parts[0] != PasswordAlgorithm ||
                !int.TryParse(parts[1], out var iterations) ||
                iterations < 100_000)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash =
                Convert.FromBase64String(parts[3]);

            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(
                actualHash,
                expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

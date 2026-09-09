using System.Net.Mail;
using System.Text.Json;
using Domio.Application.Auditing;
using Domio.Application.Users;
using Domio.Domain.Users;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Users;

public sealed class UserManagementService(
    DomioDbContext dbContext,
    IAuditService auditService) : IUserManagementService
{
    public async Task<UserManagementCreateOptions> GetCreateOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var roles = await dbContext.RoleDefinitions
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new UserRoleOption(
                x.Id,
                x.Code,
                x.NamePl,
                x.DescriptionPl))
            .ToListAsync(cancellationToken);

        var people = await dbContext.People
            .AsNoTracking()
            .Where(person =>
                person.IsActive &&
                !dbContext.UserAccounts
                    .Any(account =>
                        account.PersonId == person.Id))
            .OrderBy(person => person.LastName)
            .ThenBy(person => person.FirstName)
            .Select(person =>
                new AvailablePersonOption(
                    person.Id,
                    string.IsNullOrWhiteSpace(person.DisplayName)
                        ? (person.FirstName + " " + person.LastName).Trim()
                        : person.DisplayName!,
                    person.Email,
                    person.Phone))
            .ToListAsync(cancellationToken);

        return new UserManagementCreateOptions(
            roles,
            people);
    }

    public async Task<Guid> CreatePersonAsync(
        CreatePersonRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateName(request.FirstName, nameof(request.FirstName));
        ValidateName(request.LastName, nameof(request.LastName));

        var now = DateTime.UtcNow;

        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DisplayName = NormalizeOptional(request.DisplayName),
            Email = NormalizeOptional(request.Email),
            Phone = NormalizeOptional(request.Phone),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.People.Add(person);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M02.4.PersonCreated",
                EntityType: "Person",
                EntityId: person.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Administrator utworzył osobę bez konta logowania.",
                NewValuesJson: JsonSerializer.Serialize(new
                {
                    person.FirstName,
                    person.LastName,
                    person.DisplayName,
                    person.Email,
                    person.Phone,
                    person.IsActive
                })),
            cancellationToken);

        return person.Id;
    }

    public async Task<Guid> CreateUserAsync(
        CreateUserRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateEmail(request.Email);
        PasswordSecurity.ValidatePassword(request.Password);

        var email = request.Email.Trim();
        var normalizedEmail = NormalizeEmail(email);

        if (await dbContext.UserAccounts.AnyAsync(
                x => x.NormalizedEmail == normalizedEmail,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Konto z takim adresem e-mail już istnieje.");
        }

        var roleExists = await dbContext.RoleDefinitions
            .AnyAsync(
                x => x.Id == request.RoleDefinitionId,
                cancellationToken);

        if (!roleExists)
        {
            throw new InvalidOperationException(
                "Wybrana rola nie istnieje.");
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var now = DateTime.UtcNow;
        Person person;

        if (request.ExistingPersonId is Guid existingPersonId)
        {
            person = await dbContext.People
                .SingleOrDefaultAsync(
                    x => x.Id == existingPersonId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "Wybrana osoba nie istnieje.");

            var alreadyLinked = await dbContext.UserAccounts
                .AnyAsync(
                    x => x.PersonId == existingPersonId,
                    cancellationToken);

            if (alreadyLinked)
            {
                throw new InvalidOperationException(
                    "Wybrana osoba ma już konto użytkownika.");
            }

            if (!person.IsActive)
            {
                throw new InvalidOperationException(
                    "Nie można utworzyć konta dla nieaktywnej osoby.");
            }

            person.Email = email;
            person.Phone =
                NormalizeOptional(request.Phone) ?? person.Phone;
            person.UpdatedAtUtc = now;
        }
        else
        {
            ValidateName(request.FirstName, nameof(request.FirstName));
            ValidateName(request.LastName, nameof(request.LastName));

            person = new Person
            {
                Id = Guid.NewGuid(),
                FirstName = request.FirstName!.Trim(),
                LastName = request.LastName!.Trim(),
                DisplayName =
                    NormalizeOptional(request.DisplayName),
                Email = email,
                Phone = NormalizeOptional(request.Phone),
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.People.Add(person);
        }

        var loginName =
            await GenerateUniqueLoginAsync(
                person.FirstName,
                person.LastName,
                cancellationToken);

        var account = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            RoleDefinitionId = request.RoleDefinitionId,
            LoginName = loginName,
            NormalizedLoginName = NormalizeLogin(loginName),
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash =
                PasswordSecurity.HashPassword(request.Password),
            PasswordChangedAtUtc = now,
            IsActive = true,
            FailedLoginAttempts = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.UserAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M02.4.UserCreated",
                EntityType: "UserAccount",
                EntityId: account.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Administrator utworzył konto użytkownika z automatycznie nadanym loginem.",
                NewValuesJson: JsonSerializer.Serialize(new
                {
                    account.PersonId,
                    account.LoginName,
                    account.Email,
                    account.RoleDefinitionId,
                    account.IsActive
                })),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return account.Id;
    }

    public async Task<UserEditData?> GetEditDataAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from account in dbContext.UserAccounts.AsNoTracking()
            join person in dbContext.People.AsNoTracking()
                on account.PersonId equals person.Id
            join role in dbContext.RoleDefinitions.AsNoTracking()
                on account.RoleDefinitionId equals role.Id
            where account.Id == userId
            select new UserEditData(
                account.Id,
                person.Id,
                person.FirstName,
                person.LastName,
                person.DisplayName,
                person.Phone,
                account.LoginName,
                account.Email ?? person.Email,
                account.IsActive && person.IsActive,
                account.RoleDefinitionId,
                role.NamePl))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task UpdateUserAsync(
        UpdateUserRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateName(request.FirstName, nameof(request.FirstName));
        ValidateName(request.LastName, nameof(request.LastName));
        ValidateEmail(request.Email);

        if (request.UserId == actorUserId &&
            !request.IsActive)
        {
            throw new InvalidOperationException(
                "Nie można wyłączyć własnego konta administratora.");
        }

        var account = await dbContext.UserAccounts
            .SingleOrDefaultAsync(
                x => x.Id == request.UserId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Konto użytkownika nie istnieje.");

        var person = await dbContext.People
            .SingleAsync(
                x => x.Id == account.PersonId,
                cancellationToken);

        var normalizedEmail =
            NormalizeEmail(request.Email);

        if (await dbContext.UserAccounts.AnyAsync(
                x => x.Id != request.UserId &&
                     x.NormalizedEmail == normalizedEmail,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Konto z takim adresem e-mail już istnieje.");
        }

        var oldValues = JsonSerializer.Serialize(new
        {
            person.FirstName,
            person.LastName,
            person.DisplayName,
            PersonEmail = person.Email,
            person.Phone,
            account.LoginName,
            AccountEmail = account.Email,
            account.IsActive
        });

        var now = DateTime.UtcNow;
        var email = request.Email.Trim();

        person.FirstName = request.FirstName.Trim();
        person.LastName = request.LastName.Trim();
        person.DisplayName =
            NormalizeOptional(request.DisplayName);
        person.Email = email;
        person.Phone = NormalizeOptional(request.Phone);
        person.IsActive = request.IsActive;
        person.UpdatedAtUtc = now;

        account.Email = email;
        account.NormalizedEmail = normalizedEmail;
        account.IsActive = request.IsActive;
        account.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M02.4.UserUpdated",
                EntityType: "UserAccount",
                EntityId: account.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Administrator zmienił dane konta użytkownika.",
                OldValuesJson: oldValues,
                NewValuesJson: JsonSerializer.Serialize(new
                {
                    person.FirstName,
                    person.LastName,
                    person.DisplayName,
                    PersonEmail = person.Email,
                    person.Phone,
                    account.LoginName,
                    AccountEmail = account.Email,
                    account.IsActive
                })),
            cancellationToken);
    }

    private async Task<string> GenerateUniqueLoginAsync(
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        var firstPart = BuildLoginPart(firstName);
        var lastPart = BuildLoginPart(lastName);

        if (string.IsNullOrWhiteSpace(firstPart) ||
            string.IsNullOrWhiteSpace(lastPart))
        {
            throw new InvalidOperationException(
                "Nie można automatycznie utworzyć loginu z imienia i nazwiska.");
        }

        firstPart = firstPart[..Math.Min(firstPart.Length, 45)];
        lastPart = lastPart[..Math.Min(lastPart.Length, 45)];

        var baseLogin = $"{firstPart}_{lastPart}";
        var candidate = baseLogin;
        var suffix = 2;

        while (await dbContext.UserAccounts.AnyAsync(
                   x => x.NormalizedLoginName ==
                        NormalizeLogin(candidate),
                   cancellationToken))
        {
            candidate = $"{baseLogin}_{suffix}";
            suffix++;

            if (suffix > 9999)
            {
                throw new InvalidOperationException(
                    "Nie udało się wygenerować unikalnego loginu.");
            }
        }

        return candidate;
    }

    private static string BuildLoginPart(string value)
    {
        var trimmed = value.Trim();

        return new string(
            trimmed
                .Where(char.IsLetterOrDigit)
                .ToArray());
    }

    private static void ValidateName(
        string? value,
        string parameterName)
    {
        ValidateRequired(value, parameterName);

        if (value!.Trim().Length > 100)
        {
            throw new ArgumentException(
                "Wartość może mieć maksymalnie 100 znaków.",
                parameterName);
        }
    }

    private static void ValidateRequired(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Wartość nie może być pusta.",
                parameterName);
        }
    }

    private static void ValidateEmail(string email)
    {
        ValidateRequired(email, nameof(email));

        if (email.Trim().Length > 254)
        {
            throw new ArgumentException(
                "Adres e-mail jest zbyt długi.",
                nameof(email));
        }

        try
        {
            var parsed = new MailAddress(email.Trim());

            if (!string.Equals(
                    parsed.Address,
                    email.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException();
            }
        }
        catch (FormatException)
        {
            throw new ArgumentException(
                "Podaj poprawny adres e-mail.",
                nameof(email));
        }
    }

    private static string NormalizeLogin(string value) =>
        value.Trim().ToUpperInvariant();

    private static string NormalizeEmail(string value) =>
        value.Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}

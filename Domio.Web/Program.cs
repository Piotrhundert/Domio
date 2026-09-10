using System.Security.Claims;
using Domio.Application.Auditing;
using Domio.Application.Authorization;
using Domio.Application.Diagnostics;
using Domio.Application.Maintenance;
using Domio.Application.PersonalFinance;
using Domio.Domain.Users;
using Domio.Infrastructure;
using Domio.Infrastructure.Persistence;
using Domio.Web.Errors;
using Domio.Web.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(
    builder.Configuration,
    builder.Environment.EnvironmentName,
    builder.Environment.ContentRootPath);

builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "Domio.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy =
            CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        options.Events.OnValidatePrincipal =
            async context =>
            {
                var userIdValue =
                    context.Principal?
                        .FindFirst(ClaimTypes.NameIdentifier)?
                        .Value;

                if (!Guid.TryParse(
                        userIdValue,
                        out var userId))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(
                        CookieAuthenticationDefaults
                            .AuthenticationScheme);
                    return;
                }

                var accessService =
                    context.HttpContext.RequestServices
                        .GetRequiredService<IUserAccessService>();

                var access =
                    await accessService.GetAsync(
                        userId,
                        context.HttpContext
                            .RequestAborted);

                if (access is null)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(
                        CookieAuthenticationDefaults
                            .AuthenticationScheme);
                    return;
                }

                var claims = new List<Claim>
                {
                    new(
                        ClaimTypes.NameIdentifier,
                        access.UserId.ToString()),
                    new(
                        ClaimTypes.Name,
                        access.DisplayName),
                    new(
                        ClaimTypes.Role,
                        access.RoleCode),
                    new(
                        DomioClaimTypes.Login,
                        access.LoginName),
                    new(
                        DomioClaimTypes.PersonId,
                        access.PersonId.ToString()),
                    new(
                        DomioClaimTypes.RoleName,
                        access.RoleNamePl)
                };

                foreach (var permission in access.Permissions)
                {
                    claims.Add(
                        new Claim(
                            DomioClaimTypes.Permission,
                            permission.Code));

                    claims.Add(
                        new Claim(
                            DomioClaimTypes.PermissionScope,
                            $"{permission.Code}|{permission.ScopeCode}"));
                }

                context.ReplacePrincipal(
                    new ClaimsPrincipal(
                        new ClaimsIdentity(
                            claims,
                            CookieAuthenticationDefaults
                                .AuthenticationScheme)));
            };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy =
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

    foreach (var permission in SystemPermissions.All)
    {
        options.AddPolicy(
            permission.Code,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(
                    DomioClaimTypes.Permission,
                    permission.Code);
            });
    }
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

var isDevelopmentOrTest =
    app.Environment.IsDevelopment() ||
    app.Environment.IsEnvironment("Test");

if (isDevelopmentOrTest)
{
    using var scope = app.Services.CreateScope();
    var dbContext =
        scope.ServiceProvider.GetRequiredService<DomioDbContext>();

    await dbContext.Database.MigrateAsync();

    app.MapGet(
        "/dev/error",
        static () => ThrowTestException());

    app.MapGet("/dev/audit-test", async (
        HttpContext httpContext,
        IAuditService auditService,
        DomioDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        var auditId = await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M01.4.AuditTest",
                EntityType: "System",
                EntityId: "M01",
                ActorId:
                    app.Environment.EnvironmentName
                        .ToLowerInvariant(),
                CorrelationId:
                    httpContext.TraceIdentifier,
                Description:
                    "Kontrolowany wpis audytowy testu M01.4."),
            cancellationToken);

        var auditCount = await dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(cancellationToken);

        return Results.Ok(new
        {
            status = "AuditSaved",
            auditId,
            correlationId =
                httpContext.TraceIdentifier,
            auditCount
        });
    });

    app.MapGet("/dev/m02/roles", async (
        DomioDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        var roles = await dbContext.RoleDefinitions
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Code,
                name = x.NamePl,
                description = x.DescriptionPl,
                x.IsSystem
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            module = "M02",
            package = "M02.7",
            count = roles.Count,
            roles
        });
    });

    app.MapGet("/dev/m02/access", (
        HttpContext httpContext) =>
    {
        var permissions =
            httpContext.User
                .FindAll(DomioClaimTypes.PermissionScope)
                .Select(x => x.Value)
                .OrderBy(x => x)
                .ToArray();

        return Results.Ok(new
        {
            module = "M02",
            package = "M02.7",
            user = httpContext.User.Identity?.Name,
            role =
                httpContext.User
                    .FindFirst(DomioClaimTypes.RoleName)?
                    .Value,
            permissions
        });
    }).RequireAuthorization();

    app.MapGet(
        "/dev/m02/rbac/users-view",
        () => Results.Ok(new
        {
            status = "Allowed",
            permission = SystemPermissions.UsersView
        }))
        .RequireAuthorization(
            SystemPermissions.UsersView);

    app.MapGet("/dev/m03/foundation", async (
        HttpContext httpContext,
        IPersonalFinanceService personalFinanceService,
        CancellationToken cancellationToken) =>
    {
        var userIdValue =
            httpContext.User
                .FindFirst(ClaimTypes.NameIdentifier)?
                .Value;

        if (!Guid.TryParse(
                userIdValue,
                out var userId))
        {
            return Results.Unauthorized();
        }

        var overview =
            await personalFinanceService
                .GetOwnOverviewAsync(
                    userId,
                    cancellationToken);

        return Results.Ok(new
        {
            module = "M04",
            package = "M03.1",
            ownerPersonId =
                overview.OwnerPersonId,
            personalAccounts =
                overview.Accounts.Count,
            recentTransactions =
                overview.RecentTransactions.Count,
            availableAccountTypes =
                Domio.Domain.PersonalFinance
                    .PersonalAccountTypes.All
                    .Select(x => new
                    {
                        x.Code,
                        x.NamePl
                    })
        });
    })
    .RequireAuthorization(
        SystemPermissions.FinancePersonalViewOwn);

    app.MapPost("/dev/database/backup", async (
        HttpContext httpContext,
        IDatabaseMaintenanceService maintenanceService,
        CancellationToken cancellationToken) =>
    {
        var result =
            await maintenanceService.CreateBackupAsync(
                cancellationToken);

        return Results.Ok(new
        {
            status = "BackupCreated",
            result.FileName,
            result.SizeBytes,
            result.CreatedAtUtc,
            correlationId =
                httpContext.TraceIdentifier
        });
    });

    app.MapPost(
        "/dev/database/restore/{fileName}",
        async (
            string fileName,
            HttpContext httpContext,
            IDatabaseMaintenanceService maintenanceService,
            CancellationToken cancellationToken) =>
        {
            var result =
                await maintenanceService
                    .RestoreBackupAsync(
                        fileName,
                        cancellationToken);

            return Results.Ok(new
            {
                status = "BackupRestored",
                result.FileName,
                result.RestoredAtUtc,
                correlationId =
                    httpContext.TraceIdentifier
            });
        });

    if (app.Environment.IsEnvironment("Test"))
    {
        app.MapPost("/dev/test-data/reset", async (
            HttpContext httpContext,
            IDatabaseMaintenanceService maintenanceService,
            CancellationToken cancellationToken) =>
        {
            var result =
                await maintenanceService
                    .ResetTestDatabaseAsync(
                        cancellationToken);

            return Results.Ok(new
            {
                status = "TestDatabaseReset",
                result.SchemaVersion,
                result.InstanceId,
                result.ResetAtUtc,
                correlationId =
                    httpContext.TraceIdentifier
            });
        });
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (
    HttpContext httpContext,
    IDatabaseDiagnosticsService diagnosticsService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var diagnostics =
            await diagnosticsService.GetAsync(
                cancellationToken);

        var response = new
        {
            status = diagnostics.IsHealthy
                ? "Healthy"
                : "Unhealthy",
            application = "Domio",
            module = "M03",
            package = "M04.2",
            environment =
                app.Environment.EnvironmentName,
            correlationId =
                httpContext.TraceIdentifier,
            database = new
            {
                status = diagnostics.IsHealthy
                    ? "Healthy"
                    : "Unhealthy",
                provider = "SQLite",
                schemaVersion =
                    diagnostics.SchemaVersion,
                integrityCheck =
                    diagnostics.IntegrityCheck,
                journalMode =
                    diagnostics.JournalMode,
                foreignKeysEnabled =
                    diagnostics.ForeignKeysEnabled,
                instanceId =
                    diagnostics.InstanceId,
                appliedMigrations =
                    diagnostics.AppliedMigrations,
                pendingMigrations =
                    diagnostics.PendingMigrations,
                currentMigration =
                    diagnostics.CurrentMigration
            }
        };

        return diagnostics.IsHealthy
            ? Results.Ok(response)
            : Results.Json(
                response,
                statusCode:
                    StatusCodes
                        .Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(
            ex,
            "Rozszerzony Health Check zakończył się błędem. CorrelationId: {CorrelationId}",
            httpContext.TraceIdentifier);

        return Results.Problem(
            title: "Domio database unhealthy",
            detail:
                "Wystąpił błąd podczas diagnostyki bazy danych.",
            statusCode:
                StatusCodes.Status503ServiceUnavailable,
            extensions:
                new Dictionary<string, object?>
                {
                    ["correlationId"] =
                        httpContext.TraceIdentifier
                });
    }
}).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Home}/{action=Index}/{id?}");

app.Run();

static IResult ThrowTestException()
{
    throw new InvalidOperationException(
        "Kontrolowany błąd testowy M01.3.");
}

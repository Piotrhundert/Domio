using Domio.Application.Auditing;
using Domio.Application.Diagnostics;
using Domio.Application.Maintenance;
using Domio.Infrastructure;
using Domio.Infrastructure.Persistence;
using Domio.Web.Errors;
using Domio.Web.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(
    builder.Configuration,
    builder.Environment.EnvironmentName,
    builder.Environment.ContentRootPath);
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
                ActorId: app.Environment.EnvironmentName.ToLowerInvariant(),
                CorrelationId: httpContext.TraceIdentifier,
                Description: "Kontrolowany wpis audytowy testu M01.4."),
            cancellationToken);

        var auditCount = await dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(cancellationToken);

        return Results.Ok(new
        {
            status = "AuditSaved",
            auditId,
            correlationId = httpContext.TraceIdentifier,
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
            package = "M02.1",
            count = roles.Count,
            roles
        });
    });

    app.MapPost("/dev/database/backup", async (
        HttpContext httpContext,
        IDatabaseMaintenanceService maintenanceService,
        CancellationToken cancellationToken) =>
    {
        var result = await maintenanceService.CreateBackupAsync(
            cancellationToken);

        return Results.Ok(new
        {
            status = "BackupCreated",
            result.FileName,
            result.SizeBytes,
            result.CreatedAtUtc,
            correlationId = httpContext.TraceIdentifier
        });
    });

    app.MapPost("/dev/database/restore/{fileName}", async (
        string fileName,
        HttpContext httpContext,
        IDatabaseMaintenanceService maintenanceService,
        CancellationToken cancellationToken) =>
    {
        var result = await maintenanceService.RestoreBackupAsync(
            fileName,
            cancellationToken);

        return Results.Ok(new
        {
            status = "BackupRestored",
            result.FileName,
            result.RestoredAtUtc,
            correlationId = httpContext.TraceIdentifier
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
                await maintenanceService.ResetTestDatabaseAsync(
                    cancellationToken);

            return Results.Ok(new
            {
                status = "TestDatabaseReset",
                result.SchemaVersion,
                result.InstanceId,
                result.ResetAtUtc,
                correlationId = httpContext.TraceIdentifier
            });
        });
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapGet("/health", async (
    HttpContext httpContext,
    IDatabaseDiagnosticsService diagnosticsService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var diagnostics = await diagnosticsService
            .GetAsync(cancellationToken);

        var response = new
        {
            status = diagnostics.IsHealthy
                ? "Healthy"
                : "Unhealthy",
            application = "Domio",
            module = "M02",
            package = "M02.1",
            environment = app.Environment.EnvironmentName,
            correlationId = httpContext.TraceIdentifier,
            database = new
            {
                status = diagnostics.IsHealthy
                    ? "Healthy"
                    : "Unhealthy",
                provider = "SQLite",
                schemaVersion = diagnostics.SchemaVersion,
                integrityCheck = diagnostics.IntegrityCheck,
                journalMode = diagnostics.JournalMode,
                foreignKeysEnabled = diagnostics.ForeignKeysEnabled,
                instanceId = diagnostics.InstanceId,
                appliedMigrations = diagnostics.AppliedMigrations,
                pendingMigrations = diagnostics.PendingMigrations,
                currentMigration = diagnostics.CurrentMigration
            }
        };

        return diagnostics.IsHealthy
            ? Results.Ok(response)
            : Results.Json(
                response,
                statusCode:
                    StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(
            ex,
            "Rozszerzony Health Check zakończył się błędem. CorrelationId: {CorrelationId}",
            httpContext.TraceIdentifier);

        return Results.Problem(
            title: "Domio database unhealthy",
            detail: "Wystąpił błąd podczas diagnostyki bazy danych.",
            statusCode:
                StatusCodes.Status503ServiceUnavailable,
            extensions: new Dictionary<string, object?>
            {
                ["correlationId"] =
                    httpContext.TraceIdentifier
            });
    }
});

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

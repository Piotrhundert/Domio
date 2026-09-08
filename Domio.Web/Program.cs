using Domio.Application.Auditing;
using Domio.Application.Diagnostics;
using Domio.Infrastructure;
using Domio.Infrastructure.Persistence;
using Domio.Web.Errors;
using Domio.Web.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(builder.Configuration);
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

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DomioDbContext>();
    await dbContext.Database.MigrateAsync();

    app.MapGet("/dev/error", static () => ThrowTestException());

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
                ActorId: "development",
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
            status = diagnostics.IsHealthy ? "Healthy" : "Unhealthy",
            application = "Domio",
            module = "M01",
            environment = app.Environment.EnvironmentName,
            correlationId = httpContext.TraceIdentifier,
            database = new
            {
                status = diagnostics.IsHealthy ? "Healthy" : "Unhealthy",
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
                statusCode: StatusCodes.Status503ServiceUnavailable);
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
            statusCode: StatusCodes.Status503ServiceUnavailable,
            extensions: new Dictionary<string, object?>
            {
                ["correlationId"] = httpContext.TraceIdentifier
            });
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static IResult ThrowTestException()
{
    throw new InvalidOperationException(
        "Kontrolowany błąd testowy M01.3.");
}

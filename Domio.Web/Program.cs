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
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapGet("/health", async (
    HttpContext httpContext,
    DomioDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        if (!canConnect)
        {
            return Results.Problem(
                title: "Domio database unhealthy",
                detail: "Nie można połączyć się z bazą SQLite.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object?>
                {
                    ["correlationId"] = httpContext.TraceIdentifier
                });
        }

        var schemaVersion = await dbContext.SchemaVersions
            .AsNoTracking()
            .Where(x => x.Id == 1)
            .Select(x => x.Version)
            .SingleAsync(cancellationToken);

        return Results.Ok(new
        {
            status = "Healthy",
            application = "Domio",
            module = "M01",
            correlationId = httpContext.TraceIdentifier,
            database = new
            {
                status = "Healthy",
                provider = "SQLite",
                schemaVersion
            }
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(
            ex,
            "Health Check bazy danych zakończył się błędem. CorrelationId: {CorrelationId}",
            httpContext.TraceIdentifier);

        return Results.Problem(
            title: "Domio database unhealthy",
            detail: "Wystąpił błąd podczas sprawdzania bazy danych.",
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
    throw new InvalidOperationException("Kontrolowany błąd testowy M01.3.");
}

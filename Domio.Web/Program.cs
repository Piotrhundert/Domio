using Domio.Infrastructure;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DomioDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapGet("/health", async (
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
                statusCode: StatusCodes.Status503ServiceUnavailable);
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
        return Results.Problem(
            title: "Domio database unhealthy",
            detail: ex.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

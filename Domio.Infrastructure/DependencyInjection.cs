using Domio.Application.Auditing;
using Domio.Application.Authentication;
using Domio.Application.Diagnostics;
using Domio.Application.Maintenance;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Diagnostics;
using Domio.Infrastructure.Maintenance;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Domio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName,
        string contentRootPath)
    {
        var connectionString =
            configuration.GetConnectionString(
                "DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Brak ConnectionStrings:DefaultConnection w konfiguracji Domio.");
        }

        services.AddDbContext<DomioDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddSingleton(
            new DatabaseMaintenanceRuntime(
                environmentName,
                contentRootPath));

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<
            IAccountAuthenticationService,
            AccountAuthenticationService>();
        services.AddScoped<
            IDatabaseDiagnosticsService,
            DatabaseDiagnosticsService>();
        services.AddScoped<
            IDatabaseMaintenanceService,
            DatabaseMaintenanceService>();

        return services;
    }
}

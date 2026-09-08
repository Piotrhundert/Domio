using Domio.Application.Auditing;
using Domio.Application.Diagnostics;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Diagnostics;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Domio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Brak ConnectionStrings:DefaultConnection w konfiguracji Domio.");
        }

        services.AddDbContext<DomioDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IDatabaseDiagnosticsService, DatabaseDiagnosticsService>();

        return services;
    }
}

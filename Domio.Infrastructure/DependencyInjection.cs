﻿using Domio.Application.Auditing;
using Domio.Application.Authentication;
using Domio.Application.Authorization;
using Domio.Application.Diagnostics;
using Domio.Application.FamilyFinance;
using Domio.Application.FinancialGoals;
using Domio.Application.HouseholdFinance;
using Domio.Application.Maintenance;
using Domio.Application.Notifications;
using Domio.Application.PersonalFinance;
using Domio.Application.Profiles;
using Domio.Application.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Diagnostics;
using Domio.Infrastructure.FamilyFinance;
using Domio.Infrastructure.FinancialGoals;
using Domio.Infrastructure.HouseholdFinance;
using Domio.Infrastructure.Maintenance;
using Domio.Infrastructure.Notifications;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.PersonalFinance;
using Domio.Infrastructure.Profiles;
using Domio.Infrastructure.Users;
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
            IUserAccessService,
            UserAccessService>();
        services.AddScoped<
            IDatabaseDiagnosticsService,
            DatabaseDiagnosticsService>();
        services.AddScoped<
            IDatabaseMaintenanceService,
            DatabaseMaintenanceService>();
        services.AddScoped<
            IProfileService,
            ProfileService>();
        services.AddScoped<
            IFamilyFinanceService,
            FamilyFinanceService>();
        services.AddScoped<
            IFamilyBudgetService,
            FamilyBudgetService>();
        services.AddScoped<
            IFamilyAreaService,
            FamilyAreaService>();
        services.AddScoped<
            IFinancialGoalService,
            FinancialGoalService>();
        services.AddScoped<
            IHouseholdFinanceService,
            HouseholdFinanceService>();
        services.AddScoped<
            IHouseholdContributionAdminService,
            HouseholdContributionAdminService>();
        services.AddScoped<
            INotificationService,
            NotificationService>();
        services.AddScoped<
            IPersonalFinanceService,
            PersonalFinanceService>();
        services.AddScoped<
            IUserDirectoryService,
            UserDirectoryService>();
        services.AddScoped<
            IUserManagementService,
            UserManagementService>();
        services.AddScoped<
            IRoleDirectoryService,
            RoleDirectoryService>();

        return services;
    }
}

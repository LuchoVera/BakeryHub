using BakeryHub.Domain.Interfaces;
using BakeryHub.Modules.Tenants.Application.Interfaces;
using BakeryHub.Modules.Tenants.Application.Services;
using BakeryHub.Modules.Tenants.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace BakeryHub.Modules.Tenants.Infrastructure;

public static class TenantsModuleExtensions
{
    public static IServiceCollection AddTenantsModule(this IServiceCollection services)
    {
        services.AddScoped<ITenantManagementService, TenantManagementService>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantThemeRepository, TenantThemeRepository>();

        return services;
    }
}

using BakeryHub.Modules.Dashboard.Application.Interfaces;
using BakeryHub.Modules.Dashboard.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BakeryHub.Modules.Dashboard.Infrastructure;

public static class DashboardModuleExtensions
{
    public static IServiceCollection AddDashboardModule(this IServiceCollection services)
    {
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}

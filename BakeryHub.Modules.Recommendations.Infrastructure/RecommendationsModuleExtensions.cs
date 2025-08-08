using BakeryHub.Application.Interfaces.BackgroundServices;
using BakeryHub.Modules.Recommendations.Application.Interfaces;
using BakeryHub.Modules.Recommendations.Application.Services;
using BakeryHub.Modules.Recommendations.Domain.Interfaces;
using BakeryHub.Modules.Recommendations.Infrastructure.BackgroundServices;
using BakeryHub.Modules.Recommendations.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BakeryHub.Modules.Recommendations.Infrastructure;

public static class RecommendationsModuleExtensions
{
    public static IServiceCollection AddRecommendationsModule(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IModelRetrainingService, ModelRetrainingService>();

        if (environment.IsDevelopment())
        {
            services.AddScoped<IModelStorage, LocalFileModelStorage>();
        }
        else
        {
            services.AddScoped<IModelStorage, AzureBlobModelStorage>();
        }

        services.AddHostedService<ScheduledRecommendationRetrainingService>();

        return services;
    }
}

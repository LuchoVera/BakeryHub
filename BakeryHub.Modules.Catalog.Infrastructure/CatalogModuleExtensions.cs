using BakeryHub.Modules.Catalog.Application.Interfaces;
using BakeryHub.Modules.Catalog.Application.Services;
using BakeryHub.Modules.Catalog.Domain.Interfaces;
using BakeryHub.Modules.Catalog.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace BakeryHub.Modules.Catalog.Infrastructure;
public static class CatalogModuleExtensions
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITagService, TagService>();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITagRepository, TagRepository>();

        return services;
    }
}

using BakeryHub.Application.Interfaces;
using BakeryHub.Modules.Orders.Application.Services;
using BakeryHub.Modules.Orders.Domain.Interfaces;
using BakeryHub.Modules.Orders.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace BakeryHub.Modules.Orders.Infrastructure;

public static class OrdersModuleExtensions
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddScoped<IOrderService, OrderService>();

        services.AddScoped<IOrderRepository, OrderRepository>();

        return services;
    }
}

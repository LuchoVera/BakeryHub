using BakeryHub.Modules.Accounts.Application.Interfaces;
using BakeryHub.Modules.Accounts.Application.Services;
using BakeryHub.Modules.Accounts.Domain.Interfaces;
using BakeryHub.Modules.Accounts.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace BakeryHub.Modules.Accounts.Infrastructure;

public static class AccountsModuleExtensions
{
    public static IServiceCollection AddAccountsModule(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICustomerTenantMembershipRepository, CustomerTenantMembershipRepository>();

        return services;
    }
}

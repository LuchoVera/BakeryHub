using BakeryHub.Modules.Accounts.Domain.Models;

namespace BakeryHub.Modules.Accounts.Domain.Interfaces;

public interface ICustomerTenantMembershipRepository
{
    Task<bool> IsMemberAsync(Guid userId, Guid tenantId);
    Task AddAsync(CustomerTenantMembership membership);
}

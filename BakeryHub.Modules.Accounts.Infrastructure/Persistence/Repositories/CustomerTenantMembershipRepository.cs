using BakeryHub.Infrastructure.Persistence;
using BakeryHub.Modules.Accounts.Domain.Interfaces;
using BakeryHub.Modules.Accounts.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Modules.Accounts.Infrastructure.Persistence.Repositories;

public class CustomerTenantMembershipRepository : ICustomerTenantMembershipRepository
{
    private readonly ApplicationDbContext _context;

    public CustomerTenantMembershipRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsMemberAsync(Guid userId, Guid tenantId)
    {
        return await _context.Set<CustomerTenantMembership>()
            .AnyAsync(m => m.ApplicationUserId == userId && m.TenantId == tenantId && m.IsActive);
    }

    public async Task AddAsync(CustomerTenantMembership membership)
    {
        await _context.Set<CustomerTenantMembership>().AddAsync(membership);
    }
}

using BakeryHub.Domain.Entities;

namespace BakeryHub.Modules.Accounts.Domain.Models;

public class CustomerTenantMembership
{
    public Guid ApplicationUserId { get; set; }
    public Guid TenantId { get; set; }
    public DateTimeOffset DateJoined { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Tenant Tenant { get; set; } = null!;
}

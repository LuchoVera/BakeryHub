using BakeryHub.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
namespace BakeryHub.Modules.Accounts.Domain.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    [Required]
    [StringLength(150)]
    public required string Name { get; set; }
    public Guid? TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public virtual Tenant? AdministeredTenant { get; set; }
    public virtual ICollection<CustomerTenantMembership> TenantMemberships { get; set; } = new List<CustomerTenantMembership>();
    public string? PasswordResetToken { get; set; }
    public DateTimeOffset? PasswordResetTokenExpirationDate { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Name { get; set; }
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

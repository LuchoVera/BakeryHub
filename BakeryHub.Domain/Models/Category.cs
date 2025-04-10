namespace BakeryHub.Domain.Entities;
public class Category
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}

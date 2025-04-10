namespace BakeryHub.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public required string Subdomain { get; set; }
    public required string Name { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
    public virtual ICollection<CustomerTenantMembership> CustomerMemberships { get; set; }
                    = new List<CustomerTenantMembership>();

}

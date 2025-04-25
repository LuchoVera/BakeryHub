namespace BakeryHub.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public List<string> Images { get; set; } = new List<string>();
    public string? LeadTime { get; set; }

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public virtual Category Category { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow; 
        
}

namespace BakeryHub.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public required string Subdomain { get; set; }
    public required string Name { get; set; }
    public string? PhoneNumber { get; set; }
    public virtual TenantTheme? Theme { get; set; }
}

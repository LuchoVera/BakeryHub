namespace BakeryHub.Application.Dtos;

public class TenantPublicInfoDto
{
    public required string Name { get; set; }
    public required string Subdomain { get; set; }
    public string? PhoneNumber { get; set; }
}

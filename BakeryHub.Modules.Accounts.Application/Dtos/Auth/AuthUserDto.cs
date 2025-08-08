namespace BakeryHub.Modules.Accounts.Application.Dtos.Auth;

public class AuthUserDto
{
    public Guid UserId { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public required IList<string> Roles { get; set; }
    public Guid? AdministeredTenantId { get; set; }
    public string? AdministeredTenantSubdomain { get; set; }
    public List<Guid>? TenantMemberships { get; set; }
    public string? PhoneNumber { get; set; }
}

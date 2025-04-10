namespace BakeryHub.Application.Dtos;

public class AuthResponseDto
{
    public Guid UserId { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public required IList<string> Roles { get; set; } 
    public Guid? AdministeredTenantId { get; set; }
    public string? AdministeredTenantSubdomain { get; set; }
}

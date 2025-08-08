using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Modules.Tenants.Application.Dtos.Tenant;
public class UpdateTenantDto
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public required string Name { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;
public class UpdateTenantDto
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public required string Name { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;

public class TenantDto
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public required string Subdomain { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public required string Name { get; set; }
}

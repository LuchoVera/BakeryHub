using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Modules.Catalog.Application.Dtos;

public class CreateTagDto
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public required string Name { get; set; }
}

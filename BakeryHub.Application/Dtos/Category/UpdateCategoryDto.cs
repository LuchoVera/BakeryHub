using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;

public class UpdateCategoryDto
{
    [Required]
    [StringLength(150, MinimumLength = 3)]
    public required string Name { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;

public class CreateCategoryDto
{
    [Required]
    [StringLength(30, MinimumLength = 3)]
    public required string Name { get; set; }
}

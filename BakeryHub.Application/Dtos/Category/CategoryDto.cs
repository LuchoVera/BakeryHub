using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;

public class CategoryDto
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(150, MinimumLength = 3)]
    public required string Name { get; set; }
}

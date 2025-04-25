using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;

public class UpdateProductDto
{
    [Required]
    [StringLength(250, MinimumLength = 3)]
    public required string Name { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Price must be greater than 0.")]
    public decimal Price { get; set; }

    public List<string>? Images { get; set; }

    [StringLength(10)]
    public string? LeadTimeInput { get; set; }

    [Required]
    public Guid CategoryId { get; set; }
}

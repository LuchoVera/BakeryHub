using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;

public class OrderItemDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
    public int Quantity { get; set; }

    [Required]
    [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Price must be greater than 0.")]
    public decimal UnitPrice { get; set; }

    public string ProductName { get; set; } = string.Empty;

}

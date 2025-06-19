using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;

public class CreateManualOrderDto
{
    [Required]
    [StringLength(150, MinimumLength = 3)]
    public required string CustomerName { get; set; }

    [Required]
    [StringLength(20)]
    public required string CustomerPhoneNumber { get; set; }

    [Required]
    public DateTimeOffset DeliveryDate { get; set; }

    [Required]
    [MinLength(1)]
    public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();

    [Required]
    [Range(0.01, (double)decimal.MaxValue)]
    public decimal TotalAmount { get; set; }
}

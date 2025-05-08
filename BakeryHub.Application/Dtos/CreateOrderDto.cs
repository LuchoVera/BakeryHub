using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;

public class CreateOrderDto
{
    [Required(ErrorMessage = "Date is required.")]
    public DateTimeOffset DeliveryDate { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();

    [Required]
    [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Must be greater than 0.")]
    public decimal TotalAmount { get; set; }

}

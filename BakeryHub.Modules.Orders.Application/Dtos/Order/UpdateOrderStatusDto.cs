using System.ComponentModel.DataAnnotations;
using BakeryHub.Modules.Orders.Domain.Enums;

namespace BakeryHub.Modules.Orders.Application.Dtos.Order;

public class UpdateOrderStatusDto
{
    [Required]
    public OrderStatus NewStatus { get; set; }
}

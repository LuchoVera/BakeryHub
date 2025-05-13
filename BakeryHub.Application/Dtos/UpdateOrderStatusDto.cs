using System.ComponentModel.DataAnnotations;
using BakeryHub.Domain.Enums;

namespace BakeryHub.Application.Dtos;

public class UpdateOrderStatusDto
{
    [Required]
    public OrderStatus NewStatus { get; set; }
}

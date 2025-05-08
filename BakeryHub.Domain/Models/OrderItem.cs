using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BakeryHub.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; set; }

    [Required]
    public Guid OrderId { get; set; }
    public virtual Order Order { get; set; } = null!;

    [Required]
    public Guid ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;

    [Required]
    [MaxLength(250)]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    public int Quantity { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal => Quantity * UnitPrice;
}

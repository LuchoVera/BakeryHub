using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BakeryHub.Domain.Enums;

namespace BakeryHub.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }

    [Required]
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    public Guid? ApplicationUserId { get; set; }
    public virtual ApplicationUser? User { get; set; }

    [Required]
    public DateTimeOffset OrderDate { get; set; }

    [Required]
    public DateTimeOffset DeliveryDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

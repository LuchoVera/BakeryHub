namespace BakeryHub.Application.Dtos;

public class OrderDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ApplicationUserId { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public DateTimeOffset DeliveryDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
    public string? CustomerName { get; set; }
    public string? OrderNumber { get; set; }
    public string? CustomerPhoneNumber { get; set; }
}

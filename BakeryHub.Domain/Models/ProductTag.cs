namespace BakeryHub.Domain.Entities;

public class ProductTag
{
    public Guid ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;

    public Guid TagId { get; set; }
    public virtual Tag Tag { get; set; } = null!;
}

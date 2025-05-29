namespace BakeryHub.Application.Dtos;

public class ProductDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
    public List<string> Images { get; set; } = new List<string>();
    public string LeadTimeDisplay { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<string> TagNames { get; set; } = new List<string>();
}

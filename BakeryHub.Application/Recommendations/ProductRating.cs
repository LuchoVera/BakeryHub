using Microsoft.ML.Data;

namespace BakeryHub.Application.Recommendations;
public class ProductRating
{
    [LoadColumn(0)] public float UserId { get; set; }
    [LoadColumn(1)] public float ProductId { get; set; }
    [LoadColumn(2)] public float CategoryId { get; set; }
    [LoadColumn(3)] public bool Label { get; set; }
}

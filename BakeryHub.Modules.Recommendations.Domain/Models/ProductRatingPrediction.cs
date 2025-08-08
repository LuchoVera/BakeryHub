using Microsoft.ML.Data;

namespace BakeryHub.Modules.Recommendations.Domain.Models;

public class ProductRatingPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabFl { get; set; }
    public float Score { get; set; }
}

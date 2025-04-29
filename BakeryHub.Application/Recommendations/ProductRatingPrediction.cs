using Microsoft.ML.Data;

namespace BakeryHub.Application.Recommendations;

public class ProductRatingPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabFl { get; set; }
    public float Score { get; set; }
}

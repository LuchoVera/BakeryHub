using BakeryHub.Modules.Recommendations.Domain.Models;
using Microsoft.ML;

namespace BakeryHub.Modules.Recommendations.Application.Logic;

public class ModelTrainer
{
    private readonly MLContext _mlContext;

    public ModelTrainer(MLContext mlContext)
    {
        _mlContext = mlContext;
    }

    public ITransformer TrainModel(IDataView trainData)
    {
        if (trainData == null)
        {
            throw new ArgumentNullException(nameof(trainData), "Training data cannot be null.");
        }
        var preview = trainData.Preview();
        if (preview.RowView.Length == 0)
        {
            throw new InvalidOperationException("Cannot train model with empty training data.");
        }

        const string UserFeaturesCol = "UserFeatures";
        const string ProductFeaturesCol = "ProductFeatures";
        const string CategoryFeaturesCol = "CategoryFeatures";

        var pipeline =
            _mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "UserIdEncoded", inputColumnName: nameof(ProductRating.UserId))
            .Append(_mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "ProductIdEncoded", inputColumnName: nameof(ProductRating.ProductId)))
            .Append(_mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "CategoryIdEncoded", inputColumnName: nameof(ProductRating.CategoryId)))
            .Append(_mlContext.Transforms.Categorical.OneHotEncoding(outputColumnName: UserFeaturesCol, inputColumnName: "UserIdEncoded"))
            .Append(_mlContext.Transforms.Categorical.OneHotEncoding(outputColumnName: ProductFeaturesCol, inputColumnName: "ProductIdEncoded"))
            .Append(_mlContext.Transforms.Categorical.OneHotEncoding(outputColumnName: CategoryFeaturesCol, inputColumnName: "CategoryIdEncoded"))
            .Append(_mlContext.BinaryClassification.Trainers.FieldAwareFactorizationMachine(
                 labelColumnName: nameof(ProductRating.Label),
                 featureColumnNames: new[] { UserFeaturesCol, ProductFeaturesCol, CategoryFeaturesCol }));

        ITransformer trainedModel;
        try
        {
            trainedModel = pipeline.Fit(trainData);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ModelTrainer ERROR during Fit: {ex}");
            throw;
        }
        return trainedModel;
    }
}

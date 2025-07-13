namespace Core;

public interface IPredictor<in TContext>
    where TContext : class
{
    /// <summary>
    /// Predicts the outcome of a match based on the provided context.
    /// </summary>
    /// <param name="match">The match for which the prediction is to be made.</param>
    /// <param name="context">The context containing necessary data for prediction.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A prediction object containing the predicted scores.</returns>
    Task<Prediction> PredictAsync(Match match, TContext context, CancellationToken cancellationToken = default);
}

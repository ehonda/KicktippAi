namespace EHonda.KicktippAi.Core;

public interface IContextProvider<out TContext>
    where TContext : class
{
    /// <summary>
    /// Provides context data for prediction based on the given context.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An async enumerable of document contexts.</returns>
    IAsyncEnumerable<TContext> GetContextAsync(CancellationToken cancellationToken = default);
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoreOne.ModelPatch.Extensions;

/// <summary>
///
/// </summary>
public static class DataContextExtensions
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<TransactionState> BeginTransaction<TContext>(this TContext context, CancellationToken cancellationToken = default) where TContext : DbContext
    {
        return context.BeginTransaction(null, cancellationToken);
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    /// <param name="context"></param>
    /// <param name="logger"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<TransactionState> BeginTransaction<TContext>(this TContext context, ILogger? logger, CancellationToken cancellationToken = default) where TContext : DbContext
    {
        try
        {
            var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            return new TransactionState(transaction);
        }
        catch (Exception ex)
        {
            logger?.LogEntryX(ex, "Failed to create transaction");
            return new TransactionState(ex.InnerException?.Message ?? ex.Message);
        }
    }
}
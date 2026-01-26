using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoreOne.ModelPatch.Extensions;

/// <summary>
/// Extension methods for EF Core DbContext transaction management
/// </summary>
public static class DataContextExtensions
{
    /// <summary>
    /// Begins a new database transaction
    /// </summary>
    /// <typeparam name="TContext">EF Core DbContext type</typeparam>
    /// <param name="context">Database context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transaction state wrapper with automatic rollback on disposal</returns>
    public static Task<TransactionState> BeginTransaction<TContext>(this TContext context, CancellationToken cancellationToken = default) where TContext : DbContext
    {
        return context.BeginTransaction(null, cancellationToken);
    }

    /// <summary>
    /// Begins a new database transaction with logging support
    /// </summary>
    /// <typeparam name="TContext">EF Core DbContext type</typeparam>
    /// <param name="context">Database context</param>
    /// <param name="logger">Optional logger for transaction errors</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transaction state wrapper with automatic rollback on disposal</returns>
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
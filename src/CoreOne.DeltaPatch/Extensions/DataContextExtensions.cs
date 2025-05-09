using CoreOne.ModelPatch.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoreOne.ModelPatch.Extensions;

public static class DataContextExtensions
{
    public static Task<TransactionState> BeginTransaction<TContext>(this TContext context, CancellationToken cancellationToken = default) where TContext : DbContext
    {
        return context.BeginTransaction(null, cancellationToken);
    }

    public static async Task<TransactionState> BeginTransaction<TContext>(this TContext context, ILogger? logger, CancellationToken cancellationToken = default) where TContext : DbContext
    {
        try
        {
            var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            return new TransactionState(transaction);
        }
        catch (Exception ex)
        {
            logger?.LogEntry(ex, "Failed to create transaction");
            return new TransactionState(ex.InnerException?.Message ?? ex.Message);
        }
    }
}
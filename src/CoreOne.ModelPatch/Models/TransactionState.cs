using Microsoft.EntityFrameworkCore.Storage;

namespace CoreOne.ModelPatch.Models;

/// <summary>
/// Manages an EF Core transaction lifecycle with automatic rollback on disposal
/// </summary>
public sealed class TransactionState : IResult, IAsyncDisposable
{
    private volatile bool _Disposed;
    private IDbContextTransaction? Transaction;
    /// <summary>
    /// Indicates if the transaction has been disposed (committed, rolled back, or errored)
    /// </summary>
    public bool IsDisposed => _Disposed;
    /// <summary>
    /// Error message if transaction creation or execution failed
    /// </summary>
    public string? Message { get; }
    /// <summary>
    /// Status of the transaction operation
    /// </summary>
    public ResultType ResultType { get; }
    /// <summary>
    /// Indicates if transaction was successfully created and is ready for use
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Creates a successful transaction state
    /// </summary>
    /// <param name="transaction">Active EF Core transaction</param>
    public TransactionState(IDbContextTransaction transaction)
    {
        Transaction = transaction;
        ResultType = ResultType.Success;
        Success = true;
    }

    /// <summary>
    /// Creates a failed transaction state with error details
    /// </summary>
    /// <param name="message">Error message explaining why transaction failed</param>
    public TransactionState(string message)
    {
        _Disposed = true;
        Message = message;
        ResultType = ResultType.Fail;
        Success = false;
    }

    public static implicit operator bool(TransactionState state) => state.Success && state.Transaction is not null;

    /// <summary>
    /// Commits all pending changes in the transaction
    /// </summary>
    public async Task Commit()
    {
        await Utility.SafeAwait(Transaction?.CommitAsync());
        ClearTransaction();
    }

    /// <summary>
    /// Automatically rolls back transaction if not committed
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await Rollback();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Discards all pending changes in the transaction
    /// </summary>
    public async Task Rollback()
    {
        await Utility.SafeAwait(Transaction?.RollbackAsync());
        ClearTransaction();
    }

    private void ClearTransaction()
    {
        Interlocked.Exchange(ref _Disposed, true);
        Interlocked.Exchange(ref Transaction, null)?.Dispose();
    }
}
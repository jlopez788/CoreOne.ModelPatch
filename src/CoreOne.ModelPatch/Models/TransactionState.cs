using Microsoft.EntityFrameworkCore.Storage;

namespace CoreOne.ModelPatch.Models;

/// <summary>
///
/// </summary>
public sealed class TransactionState : IResult, IAsyncDisposable
{
    private volatile bool _Disposed;
    private IDbContextTransaction? Transaction;
    /// <summary>
    ///
    /// </summary>
    public bool IsDisposed => _Disposed;
    /// <summary>
    ///
    /// </summary>
    public string? Message { get; }
    /// <summary>
    ///
    /// </summary>
    public ResultType ResultType { get; }
    /// <summary>
    ///
    /// </summary>
    public bool Success { get; }

    /// <summary>
    ///
    /// </summary>
    /// <param name="transaction"></param>
    public TransactionState(IDbContextTransaction transaction)
    {
        Transaction = transaction;
        ResultType = ResultType.Success;
        Success = true;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="message"></param>
    public TransactionState(string message)
    {
        _Disposed = true;
        Message = message;
        ResultType = ResultType.Fail;
        Success = false;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public async Task Commit()
    {
        await Utility.SafeAwait(Transaction?.CommitAsync());
        ClearTransaction();
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        await Rollback();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
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
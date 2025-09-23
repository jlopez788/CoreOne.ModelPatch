using Microsoft.EntityFrameworkCore.Storage;

namespace CoreOne.ModelPatch.Models;

public sealed class TransactionState : IResult, IAsyncDisposable
{
    private volatile bool _Disposed;
    private IDbContextTransaction? Transaction;
    public bool IsDisposed => _Disposed;
    public string? Message { get; }
    public ResultType ResultType { get; }
    public bool Success { get; }

    public TransactionState(IDbContextTransaction transaction)
    {
        Transaction = transaction;
        ResultType = ResultType.Success;
        Success = true;
    }

    public TransactionState(string message)
    {
        _Disposed = true;
        Message = message;
        ResultType = ResultType.Fail;
        Success = false;
    }

    public async Task Commit()
    {
        await Utility.SafeAwait(Transaction?.CommitAsync());
        ClearTransaction();
    }

    public async ValueTask DisposeAsync()
    {
        await Rollback();
        GC.SuppressFinalize(this);
    }

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
using Microsoft.EntityFrameworkCore.Storage;

namespace OneCore.ModelPatch.Models;

public class TransactionState : IResult, IAsyncDisposable
{
    public string? Message { get; }
    public ResultType ResultType { get; }
    protected IDbContextTransaction? Transaction { get; private set; }
    private volatile bool _Disposed;
    public bool IsDisposed => _Disposed;

    public TransactionState(IDbContextTransaction transaction)
    {
        Transaction = transaction;
        ResultType = ResultType.Success;
    }

    public TransactionState(string message)
    {
        _Disposed = true;
        Message = message;
        ResultType = ResultType.Fail;
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
        _Disposed = true;
        Transaction?.Dispose();
        Transaction = null;
    }
}
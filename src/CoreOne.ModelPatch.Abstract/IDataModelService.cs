using Microsoft.EntityFrameworkCore;

namespace CoreOne.ModelPatch;

public interface IDataModelService<TContext> where TContext : DbContext
{
    TContext Context { get; }

    Task<PatchResult> Patch<T>(Delta<T> delta, CancellationToken cancellationToken = default) where T : class, new();

    Task<PatchResult> Patch<T>(DeltaCollection<T> items, CancellationToken cancellationToken = default) where T : class, new();

    Task<PatchResult> PatchCollection(IEnumerable<object?> items, CancellationToken cancellationToken = default);
}
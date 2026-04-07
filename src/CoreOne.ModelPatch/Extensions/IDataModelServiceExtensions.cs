using CoreOne.ModelPatch.Services;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace CoreOne.ModelPatch.Extensions;

public static class DataModelServiceExtensions
{
    /// <summary>
    /// Applies a partial update using a model instance directly
    /// </summary>
    /// <typeparam name="TModel">Entity type to patch</typeparam>
    /// <param name="model">Entity instance containing fields to patch</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public static Task<PatchResult> Patch<TContext, TModel>(
        this IDataModelService<TContext> service,
        TModel model,
        CancellationToken cancellationToken = default) where TContext : DbContext where TModel : class, new()
    {
        return service.Patch(model.ToDelta(), cancellationToken);
    }

    /// <summary>
    /// Applies a partial update using a model instance directly and lets callers adjust the generated delta first
    /// </summary>
    /// <typeparam name="TModel">Entity type to patch</typeparam>
    /// <param name="model">Entity instance containing fields to patch</param>
    /// <param name="configure">Callback for removing or editing delta fields before patching</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public static Task<PatchResult> Patch<TContext, TModel>(
        this IDataModelService<TContext> service,
        TModel model,
        Action<Delta<TModel>> configure, 
        CancellationToken cancellationToken = default) where TContext : DbContext where TModel : class, new()
    {
        return service.Patch(model.ToDelta(configure), cancellationToken);
    }

    /// <summary>
    /// Applies a partial update from a DTO or anonymous object mapped to the target entity type
    /// </summary>
    /// <typeparam name="TEntity">Entity type to patch</typeparam>
    /// <typeparam name="TDto">DTO type used as source payload</typeparam>
    /// <param name="dto">DTO payload to map into a delta</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public static Task<PatchResult> Patch<TContext, TEntity, TDto>(
        this IDataModelService<TContext> service,
        TDto? dto, 
        CancellationToken cancellationToken = default) where TContext : DbContext where TEntity : class, new()
    {
        return dto is null ?
            Task.FromResult(new PatchResult(null, 0, ResultType.Fail, $"DTO payload for {typeof(TEntity).Name} cannot be null")) :
            service.Patch(dto.ToDelta<TEntity>(), cancellationToken);
    }

    /// <summary>
    /// Applies a partial update from a DTO or anonymous object mapped to the target entity type and allows delta customization
    /// </summary>
    /// <typeparam name="TEntity">Entity type to patch</typeparam>
    /// <typeparam name="TDto">DTO type used as source payload</typeparam>
    /// <param name="dto">DTO payload to map into a delta</param>
    /// <param name="configure">Callback for removing or editing delta fields before patching</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public static Task<PatchResult> Patch<TContext, TEntity, TDto>(
        this IDataModelService<TContext> service, 
        TDto? dto,
        Action<Delta<TEntity>> configure,
        CancellationToken cancellationToken = default) where TContext : DbContext where TEntity : class, new()
    {
        return dto is null ?
            Task.FromResult(new PatchResult(null, 0, ResultType.Fail, $"DTO payload for {typeof(TEntity).Name} cannot be null")) :
            service.Patch(dto.ToDelta(configure), cancellationToken);
    }

    /// <summary>
    /// Applies a partial update from a DTO or anonymous object mapped to the target entity type using an explicit field allow-list
    /// </summary>
    /// <typeparam name="TEntity">Entity type to patch</typeparam>
    /// <typeparam name="TDto">DTO type used as source payload</typeparam>
    /// <param name="dto">DTO payload to map into a delta</param>
    /// <param name="includedProperties">Entity properties to include from the DTO mapping</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public static Task<PatchResult> Patch<TContext, TEntity, TDto>(
        this IDataModelService<TContext> service,
        TDto? dto,
        IEnumerable<Expression<Func<TEntity, object?>>> includedProperties, 
        CancellationToken cancellationToken = default) where TContext : DbContext where TEntity : class, new()
    {
        return dto is null ?
            Task.FromResult(new PatchResult(null, 0, ResultType.Fail, $"DTO payload for {typeof(TEntity).Name} cannot be null")) :
            service.Patch(dto.ToDelta<TEntity>(includedProperties.ToArray()), cancellationToken);
    }

    /// <summary>
    /// Applies partial updates to multiple entities of the same type using model instances directly
    /// </summary>
    /// <typeparam name="TModel">Entity type to patch</typeparam>
    /// <param name="items">Collection of models to patch</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public static Task<PatchResult> Patch<TContext, TModel>(
        this IDataModelService<TContext> service,
        IEnumerable<TModel?> items, 
        CancellationToken cancellationToken = default) where TContext : DbContext where TModel : class, new()
    {
        return Patch(service, items, _ => { }, cancellationToken);
    }

    /// <summary>
    /// Applies partial updates to multiple entities of the same type using model instances directly and lets callers adjust each generated delta first
    /// </summary>
    /// <typeparam name="TModel">Entity type to patch</typeparam>
    /// <param name="items">Collection of models to patch</param>
    /// <param name="configure">Callback for removing or editing delta fields before patching</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public static Task<PatchResult> Patch<TContext, TModel>(
        this IDataModelService<TContext> service,
        IEnumerable<TModel?> items, 
        Action<Delta<TModel>> configure,
        CancellationToken cancellationToken = default) where TContext : DbContext where TModel : class, new()
    {
        var deltas = new DeltaCollection<TModel>();
        foreach (var item in items.ExcludeNulls())
            deltas.Add(item.ToDelta(configure));
        return service.Patch(deltas, cancellationToken);
    }

    /// <summary>
    /// Applies partial updates to multiple entities of the same type using DTO payloads directly
    /// </summary>
    /// <typeparam name="TEntity">Entity type to patch</typeparam>
    /// <typeparam name="TDto">DTO type used as source payload</typeparam>
    /// <param name="items">DTO payloads to map and patch</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public static Task<PatchResult> Patch<TContext, TEntity, TDto>(
        this IDataModelService<TContext> service,
        IEnumerable<TDto?> items,
        CancellationToken cancellationToken = default) where TContext : DbContext where TEntity : class, new()
    {
        var deltas = new DeltaCollection<TEntity>();
        foreach (var item in items.ExcludeNulls())
            deltas.Add(item.ToDelta<TEntity>());
        return service.Patch(deltas, cancellationToken);
    }

    /// <summary>
    /// Applies partial updates to multiple entities of the same type using DTO payloads directly and an explicit field allow-list
    /// </summary>
    /// <typeparam name="TEntity">Entity type to patch</typeparam>
    /// <typeparam name="TDto">DTO type used as source payload</typeparam>
    /// <param name="items">DTO payloads to map and patch</param>
    /// <param name="includedProperties">Entity properties to include from the DTO mapping</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public static Task<PatchResult> Patch<TContext, TEntity, TDto>(
        this IDataModelService<TContext> service,
        IEnumerable<TDto?> items,
        IEnumerable<Expression<Func<TEntity, object?>>> includedProperties,
        CancellationToken cancellationToken = default) where TContext : DbContext where TEntity : class, new()
    {
        var deltas = new DeltaCollection<TEntity>();
        var selector = includedProperties.ToArray();
        foreach (var item in items.ExcludeNulls())
            deltas.Add(item.ToDelta<TEntity>(selector));
        return service.Patch(deltas, cancellationToken);
    }
}
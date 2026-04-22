using CoreOne.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;

namespace CoreOne.ModelPatch.Services;

public class DataModelService<TContext> : BaseService, IDataModelService<TContext> where TContext : DbContext
{
    protected const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
    protected static readonly ConcurrentDictionary<Type, InvokeCallback> LutProcessModel = new(1, 50);
    protected static readonly Type SetType = typeof(DbSet<>);
    protected readonly TContext Context;
    protected Type ContextType { get; }
    protected ModelOptions Options { get; init; } = default!;
    protected Data<Type, object> Sets { get; init; } = [];

    /// <summary>
    /// Initializes the service with default options from DI container
    /// </summary>
    /// <param name="services">Service provider for dependency injection</param>
    /// <param name="context">EF Core database context</param>
    public DataModelService(IServiceProvider services, TContext context) : this(services, context, services.GetRequiredService<IOptions<ModelOptions>>()) { }

    /// <summary>
    /// Initializes the service with explicit configuration
    /// </summary>
    /// <param name="services">Service provider for dependency injection</param>
    /// <param name="context">EF Core database context</param>
    /// <param name="options">Configuration for patch behavior</param>
    public DataModelService(IServiceProvider services, TContext context, IOptions<ModelOptions> options) : base(services)
    {
        var dbsets = MetaType.GetMetadatas(typeof(TContext), Flags);
        ContextType = typeof(TContext);
        Context = context;
        Options = options.Value ?? new();
        var sets = dbsets.Where(p => p.FPType.IsGenericType && p.FPType.GetGenericTypeDefinition() == SetType);
        Sets = sets.ToData(p => p.FPType.GetGenericArguments()[0], p => p.GetValue(context)!);
    }

    /// <summary>
    /// Applies a partial update to a single entity, processing nested relationships automatically
    /// </summary>
    /// <typeparam name="T">Entity type to patch</typeparam>
    /// <param name="delta">Partial data containing only properties to update</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public Task<PatchResult> Patch<T>(Delta<T> delta, CancellationToken cancellationToken = default) where T : class, new()
    {
        return ProcessPatch(() => ProcessUnknownModel(typeof(T), delta, new(), cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Applies partial updates to multiple entities of the same type in a single transaction
    /// </summary>
    /// <typeparam name="T">Entity type to patch</typeparam>
    /// <param name="items">Collection of deltas to process</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public Task<PatchResult> Patch<T>(DeltaCollection<T> items, CancellationToken cancellationToken = default) where T : class, new()
    {
        var type = typeof(T);
        var updated = new ProcessedModelCollection();
        IResult<ProcessedModelCollection> result = new Result<ProcessedModelCollection>();
        return ProcessPatch(() => items.AggregateResultAsync(result, (next, item) => ProcessUnknownModel(type, item, new(), cancellationToken)
            .SelectAsync(p => updated.AddRange(p))), cancellationToken);
    }

    /// <summary>
    /// Applies partial updates to a heterogeneous collection of entities in a single transaction
    /// </summary>
    /// <param name="items">Mixed collection of different entity types to patch</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities created or updated during the operation</returns>
    public Task<PatchResult> PatchCollection(IEnumerable<object?> items, CancellationToken cancellationToken = default)
    {
        var models = new ProcessedModelCollection();
        IResult<ProcessedModelCollection> result = new Result<ProcessedModelCollection>();
        return ProcessPatch(() => items.ExcludeNulls()
            .AggregateResultAsync(result, (next, item) => ProcessUnknownModel(item.GetType(), ToDelta(item), new(), cancellationToken)
                .SelectAsync(p => models.AddRange(p))), cancellationToken);

        static Delta ToDelta(object instance)
        {
            return NJson.Deserialize<Delta>(NJson.Serialize(instance)) ?? [];
        }
    }

    protected async Task<PatchResult<T>> Process<T>(Func<Task<IResult<T>>> callback, CancellationToken cancellationToken = default)
    {
        var strategy = Context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(callback, async (db, state, ct) => {
            await using var transaction = await Context.BeginTransaction(cancellationToken).ConfigureAwait(false);
            if (!transaction.Success)
            {
                LogResult(transaction, "Failed processing patch");
                return new PatchResult<T>(transaction);
            }

            try
            {
                var result = await callback().ConfigureAwait(false);
                if (!result.Success)
                {
                    await transaction.Rollback().ConfigureAwait(false);
                    LogResult(result, "Failed processing patch");
                    return new PatchResult<T>(result);
                }

                var rows = await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.Commit().ConfigureAwait(false);
                return new PatchResult<T>(result, rows);
            }
            catch (Exception ex)
            {
                await transaction.Rollback().ConfigureAwait(false);
                Logger?.LogEntryX(ex, "Unable to save transaction");
                var result = Result.FromException<T>(ex);
                LogResult(result, "Failed processing patch");
                return new PatchResult<T>(result);
            }
        }, null, cancellationToken);
    }

    private static object? ConvertTokenValue(Type type, object? value)
    {
        if (value is null)
            return null;
        if (type == typeof(byte[]))
        {
            return value switch {
                byte[] bytes => bytes,
                IEnumerable<byte> sequence => sequence.ToArray(),
                string encoded => Convert.FromBase64String(encoded),
                _ => value
            };
        }

        var parsed = Types.Parse(type, value);
        return parsed.Success ? parsed.Model : value;
    }

    private static InvokeCallback GetProcessModelInvoke(Type type) => LutProcessModel.GetOrAdd(type, p => {
        var method = typeof(DataModelService<TContext>).GetMethod(nameof(ProcessModel), BindingFlags.NonPublic | BindingFlags.Instance);
        method = method?.MakeGenericMethod(p);
        return MetaType.GetInvokeMethod(method);
    });

    private static bool TokenEquals(Type type, object? left, object? right)
    {
        return (left is null && right is null) || (left is not null && right is not null && (type == typeof(byte[])) ?
            ((byte[])left).SequenceEqual((byte[])right) :
            left?.Equals(right) == true);
    }

    private (NamedKey key, T model) PatchModel<T>(ModelContext context, T model, Delta delta, NamedKey parentKey, bool isnew)
    {
        var modelKey = new NamedKey();
        var icore = typeof(ICoreId<,>);
        var ignore = new HashSet<string>(Options.IgnoreFields.Get(context.Type) ?? [], MStringComparer.OrdinalIgnoreCase);
        ignore.AddRange(context.ConcurrencyTokens.Select(p => p.Name));
        ignore.AddRange(context.Keys
            .SelectMany(p => p.Where(k => k.IsPrimaryKey)
                .Select(k => k.Name)
                .ToHashSet(MStringComparer.OrdinalIgnoreCase)));
        context.Properties.Where(p => !ignore.Contains(p.Key) && (p.Value.FPType.IsEnum || p.Value.FPType.IsPrimitive() || p.Value.FPType.Implements(icore)))
            .Select(p => p.Value)
            .Each(ProcessProperty);
        context.GetPrimaryKeys()
            .SelectMany(p => p)
            .Where(p => p.FPType == Types.Guid || p.FPType == Types.NGuid)
            .Each(CheckPrimaryKeys);

        return (modelKey, model);

        void ProcessProperty(Metadata metadata)
        {
            var comparer = Options.Comparer.Get(metadata.FPType);
            var name = Options.GetPreferredName(metadata);
            if (delta.TryGetValue(name, out var value))
            {
                var nextValue = Types.Parse(metadata.FPType, value);
                if (nextValue.Success && comparer?.Equals(nextValue.Model, metadata.GetValue(model)) != true)
                    metadata.SetValue(model, nextValue.Model);
            }
            if (context.Link is not null && metadata.Name.Matches(context.Link.ChildProperty) && context.Link.ParentProperties.Any(p => parentKey.ContainsKey(p.Name)))
            {
                context.Link.ParentProperties
                    .Where(p => parentKey.ContainsKey(p.Name))
                    .Each(key => metadata.SetValue(model, parentKey[key.Name]));
            }
        }
        void CheckPrimaryKeys(Metadata metadata)
        {
            var key = metadata.GetValue(model);
            if (isnew || key is null || (key is Guid pkey && pkey == Guid.Empty))
            {
                if (delta.TryGetValue(metadata.Name, out key))
                {
                    var parsed = Types.Parse(metadata.FPType, key);
                    key = parsed.Success ? parsed.Model : Options.KeyGenerator.Create();
                }
                else
                    key = Options.KeyGenerator.Create();
                modelKey.Set(metadata.Name, key);
                metadata.SetValue(model, key);
            }
            else if (!ignore.Contains(metadata.Name) && delta.TryGetValue(metadata.Name, out var value))
            {
                var comparer = Options.Comparer.Get(metadata.FPType);
                var nextValue = Types.Parse(metadata.FPType, value);
                if (nextValue.Success && comparer?.Equals(nextValue.Model, metadata.GetValue(model)) != true)
                {
                    key = nextValue.Model;
                    metadata.SetValue(model, nextValue.Model);
                }
            }

            modelKey.Set(metadata.Name, key);
        }
    }

    private Task<IResult<ProcessedModelCollection>> ProcessChildrenModels(ModelContext context, Delta delta, NamedKey parentKey, CancellationToken cancellationToken)
    {
        var models = new ProcessedModelCollection();
        IResult<ProcessedModelCollection> result = new Result<ProcessedModelCollection>(models);
        return context.GetChildren(Options, delta)
                .AggregateAsync(result, (_, child) =>
                  child.Value.AggregateResultAsync(_, async (__, inner) => await ProcessUnknownModel(child.Key, inner, parentKey, cancellationToken)
                    .SelectAsync(p => models.AddRange(p))));
    }

    private async Task<IResult<ProcessedModelCollection>> ProcessModel<T>(ModelContext context, Delta delta, NamedKey parentKey, CancellationToken cancellationToken) where T : class, new()
    {
        var type = typeof(T);
        var set = (DbSet<T>?)Sets.Get(type);
        return set is not null ?
            await context.GetPrimaryKeysExpression<T>(Options, delta)
                .SelectResultAsync(ProcessExpression) :
            Result.Fail<ProcessedModelCollection>($"{ContextType} does not contain DbSet of type {type.FullName}");

        async Task<IResult<ProcessedModelCollection>> ProcessExpression(Expression<Func<T, bool>> expression)
        {
            var localSource = set.Local.AsQueryable().FirstOrDefault(expression);
            var entry = localSource is not null ? Context.Entry(localSource) : null;
            var source = await set.FirstOrDefaultAsync(expression, cancellationToken).ConfigureAwait(false);
            if (localSource is not null && source is null)
            { // We matched with a model not yet sent to db but somehow is dup?
                return new Result<ProcessedModelCollection>([new ModelState(localSource, CrudType.Read)]);
            }

            var isnew = source is null;
            if (source is not null)
            {
                var concurrencyError = ValidateConcurrencyTokens(context, source, delta);
                if (concurrencyError is not null)
                    return Result.Fail<ProcessedModelCollection>(concurrencyError);
            }

            var models = new ProcessedModelCollection();
            var (key, model) = PatchModel(context, source ?? new(), delta, parentKey, isnew);
            models.Add(new ModelState(model, isnew ? CrudType.Created : CrudType.Updated));
            return await model.ValidateModel(ServiceProvider, true)
                .SelectResultAsync(async () => {
                    var callback = isnew ? set.Add : new Func<T, EntityEntry<T>>(set.Update);
                    callback.Invoke(model);
                    var next = await ProcessChildrenModels(context, delta, key, cancellationToken);
                    return next.Select(p => models.AddRange(p));
                });
        }
    }

    private async Task<PatchResult> ProcessPatch(Func<Task<IResult<ProcessedModelCollection>>> callback, CancellationToken cancellationToken = default)
    {
        var result = await Process(callback, cancellationToken).ConfigureAwait(false);
        return new PatchResult(result, result.Rows);
    }

    private async Task<IResult<ProcessedModelCollection>> ProcessUnknownModel(ModelContext context, Delta delta, NamedKey parentKey, CancellationToken cancellationToken = default)
    {
        if (!Sets.ContainsKey(context.Type))
            return Result.Fail<ProcessedModelCollection>($"{ContextType} does not contain DbSet of type {context}");
        if (cancellationToken.IsCancellationRequested)
            return Result.Fail<ProcessedModelCollection>("Token has been cancelled");

        var deltaValidationError = ValidateDeltaKeys(context, delta);
        if (deltaValidationError is not null)
            return Result.Fail<ProcessedModelCollection>(deltaValidationError);

        var callback = GetProcessModelInvoke(context.Type);
        try
        {
            var oresult = await callback.InvokeAsync(this, [context, delta, parentKey, cancellationToken]);
            return oresult is IResult<ProcessedModelCollection> result ? result : Result.Fail<ProcessedModelCollection>($"Unknown errors");
        }
        catch (Exception ex)
        {
            Logger.LogEntryX(ex, $"Processing unknown model: {context.Type}");
            throw;
        }
    }

    private string? ValidateConcurrencyTokens<T>(ModelContext context, T model, Delta delta)
    {
        if (context.ConcurrencyTokens.Count == 0)
            return null;

        var providedTokens = context.ConcurrencyTokens
            .Where(p => delta.ContainsKey(Options.GetPreferredName(p)) || delta.ContainsKey(p.Name))
            .ToList();

        if (Options.RequireConcurrencyTokenForUpdates && providedTokens.Count == 0)
            return $"Concurrency token is required for updates to {context.Type.Name}";
        if (!Options.ValidateConcurrencyTokens || providedTokens.Count == 0)
            return null;

        foreach (var token in providedTokens)
        {
            var fieldName = Options.GetPreferredName(token);
            var rawValue = delta.TryGetValue(fieldName, out object? value) ? value : delta[token.Name];
            var expected = token.GetValue(model);
            var incoming = ConvertTokenValue(token.FPType, rawValue);
            if (!TokenEquals(token.FPType, expected, incoming))
                return $"Concurrency token mismatch for {context.Type.Name}.{token.Name}";
        }

        return null;
    }

    private string? ValidateDeltaKeys(ModelContext context, Delta delta)
    {
        if (!Options.StrictPropertyMatching || delta.Count == 0)
            return null;

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var metadata in context.Properties.Values)
        {
            allowed.Add(metadata.Name);
            allowed.Add(Options.GetPreferredName(metadata));
        }

        var unknown = delta.Keys.Where(p => !allowed.Contains(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return unknown.Count > 0 ?
            $"Unknown fields for {context.Type.Name}: {string.Join(", ", unknown.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))}" :
            null;
    }
}
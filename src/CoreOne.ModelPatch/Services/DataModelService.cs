using CoreOne.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CoreOne.ModelPatch.Services;

/// <summary>
///
/// </summary>
/// <typeparam name="TContext"></typeparam>
public class DataModelService<TContext> : BaseService where TContext : DbContext
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
    private static readonly ConcurrentDictionary<Type, InvokeCallback> LutProcessModel = new(1, 50);
    private static readonly Type SetType = typeof(DbSet<>);
    private readonly TContext Context;
    private readonly ModelOptions Options;
    private readonly Data<Type, object> Sets;

    /// <summary>
    ///
    /// </summary>
    /// <param name="services"></param>
    /// <param name="context"></param>
    public DataModelService(IServiceProvider services, TContext context) : this(services, context, services.GetRequiredService<IOptions<ModelOptions>>()) { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="services"></param>
    /// <param name="context"></param>
    /// <param name="options"></param>
    public DataModelService(IServiceProvider services, TContext context, IOptions<ModelOptions> options) : base(services)
    {
        var dbsets = MetaType.GetMetadatas(typeof(TContext), Flags);
        Context = context;
        Options = options.Value ?? new();
        var sets = dbsets.Where(p => p.FPType.IsGenericType && p.FPType.GetGenericTypeDefinition() == SetType);
        Sets = sets.ToData(p => p.FPType.GetGenericArguments()[0], p => p.GetValue(context)!);
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="delta"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IResult<ProcessedModelCollection>> Patch<T>(Delta<T> delta, CancellationToken cancellationToken = default) where T : class, new()
    {
        return Process(() => ProcessUnknownModel(typeof(T), delta, new(), cancellationToken), cancellationToken);
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="items"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IResult<ProcessedModelCollection>> Patch<T>(DeltaCollection<T> items, CancellationToken cancellationToken = default) where T : class, new()
    {
        var type = typeof(T);
        var updated = new ProcessedModelCollection();
        IResult<ProcessedModelCollection> result = new Result<ProcessedModelCollection>();
        return Process(() => items.AggregateResultAsync(result, (next, item) => ProcessUnknownModel(type, item, new(), cancellationToken)
            .SelectAsync(p => updated.AddRange(p))), cancellationToken);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="items"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IResult<ProcessedModelCollection>> PatchCollection(IEnumerable<object> items, CancellationToken cancellationToken = default)
    {
        var models = new ProcessedModelCollection();
        IResult<ProcessedModelCollection> result = new Result<ProcessedModelCollection>();
        return Process(() => items.ExcludeNulls()
            .AggregateResultAsync(result, (next, item) => ProcessUnknownModel(item.GetType(), ToDelta(item), new(), cancellationToken)
                .SelectAsync(p => models.AddRange(p))), cancellationToken);

        static Delta ToDelta(object instance) => Utility.DeserializeObject<Delta>(Utility.Serialize(instance)) ?? [];
    }

    private static InvokeCallback GetProcessModelInvoke(Type type) => LutProcessModel.GetOrAdd(type, p => {
        var method = typeof(DataModelService<TContext>).GetMethod(nameof(ProcessModel), BindingFlags.NonPublic | BindingFlags.Instance);
        method = method?.MakeGenericMethod(p);
        return MetaType.GetInvokeMethod(method);
    });

    private async Task<IResult<T>> Process<T>(Func<Task<IResult<T>>> callback, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Context.BeginTransaction(Logger, cancellationToken).ConfigureAwait(false);
        var result = await transaction.SelectResultAsync(callback)
            .SelectResultAsync(saveAsync);
        LogResult(result, "Failed processing patch");
        return result;

        async Task<IResult<T>> saveAsync(T model)
        {
            try
            {
                var rows = await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.Commit().ConfigureAwait(false);
                return new Result<T>(model);
            }
            catch (Exception ex)
            {
                await transaction.Rollback();
                Logger?.LogEntryX(ex, "Unable to save transaction");
                return Result.FromException<T>(ex);
            }
        }
    }

    private (NamedKey key, T model) PatchModel<T>(ModelContext context, T model, Delta delta, NamedKey parentKey, bool isnew)
    {
        var modelKey = new NamedKey();
        var ignore = new HashSet<string>(Options.IgnoreFields.Get(context.Type) ?? [], MStringComparer.OrdinalIgnoreCase);
        ignore.AddRange(context.Keys
            .SelectMany(p => p.Where(k => k.IsPrimaryKey)
                .Select(k => k.Name)
                .ToHashSet(MStringComparer.OrdinalIgnoreCase)));
        context.Properties.Where(p => !ignore.Contains(p.Key) && (p.Value.FPType.IsEnum || p.Value.FPType.IsPrimitive()))
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
        return context.GetChildren(delta)
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
            Result.Fail<ProcessedModelCollection>($"{typeof(TContext)} does not contain DbSet of type {type.FullName}");

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

    private async Task<IResult<ProcessedModelCollection>> ProcessUnknownModel(ModelContext context, Delta delta, NamedKey parentKey, CancellationToken cancellationToken = default)
    {
        if (!Sets.ContainsKey(context.Type))
            return Result.Fail<ProcessedModelCollection>($"{typeof(TContext)} does not contain DbSet of type {context}");
        if (cancellationToken.IsCancellationRequested)
            return Result.Fail<ProcessedModelCollection>("Token has been cancelled");

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
}
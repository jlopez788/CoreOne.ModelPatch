using CoreOne.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

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
    /// <param name="items"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IResult<List<T>>> Patch<T>(DeltaCollection<T> items, CancellationToken cancellationToken = default) where T : class, new()
    {
        var type = typeof(T);
        var updated = new List<T>(items.Count);
        return Patch(() => items.AggregateResultAsync((IResult<object>)new Result<object>(), (next, item) => ProcessUnknownModel(type, item, new(), cancellationToken)
            .OnSuccessAsync(p => updated.Add((T)p)))
            .SelectAsync(p => updated), cancellationToken);
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="delta"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IResult<T>> Patch<T>(Delta<T> delta, CancellationToken cancellationToken = default) where T : class, new()
    {
        return Patch(() => ProcessUnknownModel(typeof(T), delta, new(), cancellationToken)
            .SelectAsync(p => (T)p), cancellationToken);
    }

    private static InvokeCallback GetProcessModelInvoke(Type type) => LutProcessModel.GetOrAdd(type, p => {
        var method = typeof(DataModelService<TContext>).GetMethod(nameof(ProcessModel), BindingFlags.NonPublic | BindingFlags.Instance);
        method = method?.MakeGenericMethod(p);
        return MetaType.GetInvokeMethod(method);
    });

    private async Task<IResult<T>> Patch<T>(Func<Task<IResult<T>>> callback, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Context.BeginTransaction(Logger, cancellationToken).ConfigureAwait(false);
        var result = await transaction.SelectResultAsync(callback)
            .SelectAsync(async p => {
                var rows = await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.Commit().ConfigureAwait(false);
                return p;
            });
        LogResult(result, "Failed processing patch");
        return result;
    }

    private (NamedKey key, T model) PatchModel<T>(ModelContext context, T model, Delta delta, NamedKey parentKey, bool isnew)
    {
        var modelKey = new NamedKey();
        var ignore = new HashSet<string>(Options.IgnoreFields.Get(context.Type) ?? [], MStringComparer.OrdinalIgnoreCase);
        ignore.AddRange(context.Keys
            .SelectMany(p => p.Where(k => k.IsPrimaryKey)
                .Select(k => k.Name)
                .ToHashSet(MStringComparer.OrdinalIgnoreCase)));
        context.Properties.Where(p => p.Value.FPType.IsPrimitive() && !ignore.Contains(p.Key))
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
            if (delta.TryGetValue(metadata.Name, out var value))
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

    private Task<IResult> ProcessChildrenModels(ModelContext context, Delta delta, NamedKey parentKey, CancellationToken cancellationToken)
    {
        return context.GetChildren(delta)
                .AggregateAsync(Result.Ok, (_, child) =>
                  child.Value.AggregateResultAsync(_, async (__, inner) => await ProcessUnknownModel(child.Key, inner, parentKey, cancellationToken)));
    }

    private async Task<IResult<T>> ProcessModel<T>(ModelContext context, Delta delta, NamedKey parentKey, CancellationToken cancellationToken) where T : class, new()
    {
        var type = typeof(T);
        var set = (DbSet<T>?)Sets.Get(type);
        return set is not null ?
            await context.GetPrimaryKeysExpression<T>(delta)
                .SelectResultAsync(ProcessExpression) :
            Result.Fail<T>($"{typeof(TContext)} does not contain DbSet of type {type.FullName}");

        async Task<IResult<T>> ProcessExpression(Expression<Func<T, bool>>? expression)
        {
            Debug.Assert(expression is not null, $"{nameof(expression)} is null. It should not be null");

            var source = await set.FirstOrDefaultAsync(expression, cancellationToken).ConfigureAwait(false);
            var isnew = source is null;
            var (key, model) = PatchModel(context, source ?? new(), delta, parentKey, isnew);
            return await model.ValidateModel(ServiceProvider, true)
                .SelectResultAsync(async () => {
                    var callback = isnew ? set.Add : new Func<T, EntityEntry<T>>(set.Update);
                    callback.Invoke(model);
                    var next = await ProcessChildrenModels(context, delta, key, cancellationToken);
                    return next.Select(() => model);
                });
        }
    }

    private async Task<IResult<object>> ProcessUnknownModel(ModelContext context, Delta delta, NamedKey parentKey, CancellationToken cancellationToken = default)
    {
        if (!Sets.ContainsKey(context.Type))
            return Result.Fail<object>($"{typeof(TContext)} does not contain DbSet of type {context}");
        if (cancellationToken.IsCancellationRequested)
            return Result.Fail<object>("Token has been cancelled");

        var oresult = await GetProcessModelInvoke(context.Type).InvokeAsync(this, [context, delta, parentKey, cancellationToken]);
        var meta = MetaType.GetMetadata(oresult?.GetType(), nameof(IResult<object>.Model));
        return oresult is IResult result ? new Result<object>(meta.GetValue(oresult), result.ResultType) {
            Message = result.Message
        } : Result.Fail<object>("Unknown errors");
    }
}
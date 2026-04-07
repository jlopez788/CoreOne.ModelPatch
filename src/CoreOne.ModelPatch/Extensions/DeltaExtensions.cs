namespace CoreOne.ModelPatch.Extensions;

/// <summary>
/// Extension methods for converting models to deltas
/// </summary>
public static class DeltaExtensions
{
    /// <summary>
    /// Converts an entity instance into a delta for partial updates
    /// </summary>
    /// <typeparam name="TModel">Entity type</typeparam>
    /// <param name="model">Entity instance to convert</param>
    /// <returns>Delta containing all non-null properties from the model</returns>
    public static Delta<TModel> ToDelta<TModel>(this TModel? model) where TModel : class, new()
    {
        return model is null ? [] : Process<TModel, Delta<TModel>>(model, []);
    }

    /// <summary>
    /// Converts a DTO or anonymous object into a delta for a target entity type using matching property names
    /// </summary>
    /// <typeparam name="TEntity">Target entity type for the resulting delta</typeparam>
    /// <param name="model">Source DTO or anonymous object</param>
    /// <returns>Delta containing source values keyed for the target entity type</returns>
    public static Delta<TEntity> ToDelta<TEntity>(this object? model) where TEntity : class, new()
    {
        return model is null ? [] : Process<object, Delta<TEntity>>(model, []);
    }

    /// <summary>
    /// Converts a DTO or anonymous object into a delta for a target entity type and limits the included fields
    /// </summary>
    /// <typeparam name="TEntity">Target entity type for the resulting delta</typeparam>
    /// <param name="model">Source DTO or anonymous object</param>
    /// <param name="includedFields">Field names to keep in the delta</param>
    /// <returns>Filtered delta for the target entity type</returns>
    public static Delta<TEntity> ToDelta<TEntity>(this object? model, params string[] includedFields) where TEntity : class, new()
    {
        return Filter(ToDelta<TEntity>(model), includedFields);
    }

    /// <summary>
    /// Converts a DTO or anonymous object into a delta for a target entity type and limits the included fields
    /// </summary>
    /// <typeparam name="TEntity">Target entity type for the resulting delta</typeparam>
    /// <param name="model">Source DTO or anonymous object</param>
    /// <param name="includedProperties">Entity properties to keep in the delta</param>
    /// <returns>Filtered delta for the target entity type</returns>
    public static Delta<TEntity> ToDelta<TEntity>(this object? model, params Expression<Func<TEntity, object?>>[] includedProperties) where TEntity : class, new()
    {
        return Filter(ToDelta<TEntity>(model), includedProperties.Select(GetPropertyName));
    }

    /// <summary>
    /// Converts a model into a delta and applies additional delta customization before patching
    /// </summary>
    /// <typeparam name="TEntity">Target entity type for the resulting delta</typeparam>
    /// <param name="model">Source DTO or entity instance</param>
    /// <param name="configure">Callback for removing or editing fields in the delta</param>
    /// <returns>Configured delta for the target entity type</returns>
    public static Delta<TEntity> ToDelta<TEntity>(this object? model, Action<Delta<TEntity>> configure) where TEntity : class, new()
    {
        var delta = ToDelta<TEntity>(model);
        configure(delta);
        return delta;
    }

    /// <summary>
    /// Converts multiple entity instances into a delta collection for batch operations
    /// </summary>
    /// <typeparam name="TModel">Entity type</typeparam>
    /// <param name="models">Collection of entities to convert</param>
    /// <returns>Delta collection ready for batch patching</returns>
    public static DeltaCollection<TModel> ToDeltaCollection<TModel>(this IEnumerable<TModel?>? models) where TModel : class, new()
    {
        var next = models?.ExcludeNulls().ToList() ?? [];
        return Process<List<TModel>, DeltaCollection<TModel>>(next, []);
    }

    private static TDelta Process<TModel, TDelta>(TModel model, TDelta defaultValue) => NJson.Deserialize<TDelta>(NJson.Serialize(model)) ?? defaultValue;

    private static Delta<TEntity> Filter<TEntity>(Delta<TEntity> delta, IEnumerable<string> includedFields) where TEntity : class, new()
    {
        var keep = new HashSet<string>(includedFields.Where(p => !string.IsNullOrWhiteSpace(p)), StringComparer.OrdinalIgnoreCase);
        if (keep.Count == 0)
            return delta;

        foreach (var key in delta.Keys.Where(p => !keep.Contains(p)).ToList())
            delta.Remove(key);
        return delta;
    }

    private static string GetPropertyName<TEntity>(Expression<Func<TEntity, object?>> expression) where TEntity : class, new()
    {
        var member = expression.Body switch {
            MemberExpression direct => direct,
            UnaryExpression { Operand: MemberExpression converted } => converted,
            _ => throw new ArgumentException("Included properties must be simple member access expressions", nameof(expression))
        };

        return member.Member.Name;
    }
}
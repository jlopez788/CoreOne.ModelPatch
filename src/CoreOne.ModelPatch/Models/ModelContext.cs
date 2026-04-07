using Microsoft.EntityFrameworkCore;

namespace CoreOne.ModelPatch.Models;

/// <summary>
/// Cached metadata for an entity type including properties, keys, and relationships
/// </summary>
public class ModelContext
{
    private readonly int Code;
    private readonly string Name;
    /// <summary>
    /// Concurrency token properties discovered via <see cref="TimestampAttribute"/> or <see cref="ConcurrencyCheckAttribute"/>
    /// </summary>
    public IReadOnlyList<Metadata> ConcurrencyTokens { get; }
    /// <summary>
    /// Indicates whether the entity has valid key properties for identification
    /// </summary>
    public bool IsValid { get; }
    /// <summary>
    /// Primary and unique keys used to identify existing entities (outer list is OR, inner list is AND)
    /// </summary>
    public List<List<ModelKey>> Keys { get; }
    /// <summary>
    /// Parent-child relationship information for foreign key injection, or null if processing a root entity
    /// </summary>
    public ModelLink? Link { get; }
    /// <summary>
    /// All properties on the entity type, indexed by name
    /// </summary>
    public Data<string, Metadata> Properties { get; }
    /// <summary>
    /// The entity CLR type
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Analyzes an entity type to discover its keys, properties, and relationships
    /// </summary>
    /// <param name="type">Entity type to analyze</param>
    /// <param name="link">Optional parent relationship for child entity processing</param>
    public ModelContext(Type type, ModelLink? link = null)
    {
        var properties = MetaType.GetMetadatas(type);
        var classId = $"{type.Name}Id";
        var classKey = $"{type.Name}Key";
        Type = type;
        Name = type.FullName!;
        Link = link;
        Code = (Type, Link).GetHashCode();
        Properties = properties.ToDictionary();
        ConcurrencyTokens = [.. properties.Where(p => p.GetCustomAttribute<TimestampAttribute>() is not null || p.GetCustomAttribute<ConcurrencyCheckAttribute>() is not null)];
        var pKey = properties.Where(p => p.GetCustomAttribute<KeyAttribute>() is not null)
            .Select(p => new ModelKey(p.Name, true))
            .ToList();
        var uniqueNames = type.GetCustomAttributes<IndexAttribute>()
             .Where(p => p.IsUnique)
             .Select(p => p.PropertyNames.Select(n => new ModelKey(n, false)).ToList());
        Keys = [pKey, .. uniqueNames];
        if (Keys.Count == 0)
        {
            Keys.AddRange(properties.Where(p => p.Name.MatchesAny("Id", "Key", classId, classKey))
                .Select(p => new ModelKey(p.Name, true))
                .ToList());
        }
        IsValid = Keys.Count > 0;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="type"></param>
    public static implicit operator ModelContext(Type type) => new(type);

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? obj) => obj is ModelContext other && (ReferenceEqualityComparer.Default.Equals(this, obj) || Type == other.Type);

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() => Code;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <returns></returns>
    public override string ToString() => Name;
}
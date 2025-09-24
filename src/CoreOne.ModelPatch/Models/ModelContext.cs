using Microsoft.EntityFrameworkCore;

namespace CoreOne.ModelPatch.Models;

/// <summary>
///
/// </summary>
public class ModelContext
{
    private readonly int Code;
    private readonly string Name;
    /// <summary>
    ///
    /// </summary>
    public bool IsValid { get; }
    /// <summary>
    ///
    /// </summary>
    public List<List<ModelKey>> Keys { get; }
    /// <summary>
    ///
    /// </summary>
    public ModelLink? Link { get; }
    /// <summary>
    ///
    /// </summary>
    public Data<string, Metadata> Properties { get; }
    /// <summary>
    ///
    /// </summary>
    public Type Type { get; }

    /// <summary>
    ///
    /// </summary>
    /// <param name="type"></param>
    /// <param name="link"></param>
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
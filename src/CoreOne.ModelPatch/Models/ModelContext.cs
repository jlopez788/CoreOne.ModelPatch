namespace CoreOne.ModelPatch.Models;

public class ModelContext
{
    private readonly int Code;
    private readonly string Name;
    public bool IsValid { get; }
    public HashSet<string> Keys { get; }
    public ModelLink? Link { get; }
    public Data<string, Metadata> Properties { get; }
    public Type Type { get; }

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
        Keys = properties.Where(p => p.GetCustomAttribute<KeyAttribute>() is not null)
            .Select(p => p.Name)
            .ToHashSet(MStringComparer.OrdinalIgnoreCase);
        if (Keys.Count == 0)
        {
            Keys.AddRange(properties.Where(p => p.Name.MatchesAny("Id", "Key", classId, classKey))
                .Select(p => p.Name));
        }
        IsValid = Keys.Count > 0;
    }

    public static implicit operator ModelContext(Type type) => new(type);

    public override bool Equals(object? obj) => obj is ModelContext other && (ReferenceEqualityComparer.Default.Equals(this, obj) || Type == other.Type);

    public override int GetHashCode() => Code;

    public override string ToString() => Name;
}
using CoreOne.ModelPatch.Services;

namespace CoreOne.ModelPatch.Models;

/// <summary>
/// Configuration options for controlling delta processing and entity patching behavior
/// </summary>
public class ModelOptions
{
    /// <summary>
    /// Type-specific comparers for determining property value equality
    /// </summary>
    public Data<Type, IEqualityComparer> Comparer { get; } = [];
    /// <summary>
    /// Properties to exclude from patch operations, organized by entity type
    /// </summary>
    public DataHashSet<Type, string> IgnoreFields { get; }
    /// <summary>
    /// Strategy for generating primary key values when creating new entities
    /// </summary>
    public IKeyGenerator KeyGenerator { get; set; } = new GuidGenerator();
    /// <summary>
    /// Custom function to map property metadata to delta property names (e.g., for JSON attribute support)
    /// </summary>
    public Func<Metadata, string>? NameResolver { get; set; }

    /// <summary>
    /// Initializes options with default configuration
    /// </summary>
    public ModelOptions()
    {
        IgnoreFields = new(ReferenceEqualityComparer<Type>.Default, StringComparer.OrdinalIgnoreCase);
        Comparer = new Data<Type, IEqualityComparer>() {
            DefaultKey = Types.Object,
            [Types.String] = StringComparer.OrdinalIgnoreCase,
            [Types.Object] = ReferenceEqualityComparer.Default
        };
    }
}
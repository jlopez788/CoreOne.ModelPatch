using System.Collections;

namespace CoreOne.ModelPatch.Abstract.Models;

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
    /// Per-type key generators. Checked before <see cref="KeyGenerator"/> when resolving a generator for a specific key type.
    /// </summary>
    public Data<Type, IKeyGenerator> KeyGenerators { get; } = [];
    /// <summary>
    /// Custom function to map property metadata to delta property names (e.g., for JSON attribute support)
    /// </summary>
    public Func<Metadata, string>? NameResolver { get; set; }

    /// <summary>
    /// Enforces strict delta key matching so unknown fields fail fast instead of being ignored
    /// </summary>
    public bool StrictPropertyMatching { get; set; }
    /// <summary>
    /// Validates provided concurrency token values on updates to detect stale writes
    /// </summary>
    public bool ValidateConcurrencyTokens { get; set; } = true;
    /// <summary>
    /// Requires at least one concurrency token in update deltas when concurrency tokens are configured on the model
    /// </summary>
    public bool RequireConcurrencyTokenForUpdates { get; set; }

    /// <summary>
    /// Plugin types to exclude from the patch pipeline for this context
    /// </summary>
    public HashSet<Type> ExcludePlugins { get; set; } = [];

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
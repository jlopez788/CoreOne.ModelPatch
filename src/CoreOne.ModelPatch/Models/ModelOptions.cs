using CoreOne.ModelPatch.Services;

namespace CoreOne.ModelPatch.Models;

/// <summary>
///
/// </summary>
public class ModelOptions
{
    /// <summary>
    ///
    /// </summary>
    public Data<Type, IEqualityComparer> Comparer { get; } = [];
    /// <summary>
    ///
    /// </summary>
    public DataHashSet<Type, string> IgnoreFields { get; }
    /// <summary>
    ///
    /// </summary>
    public IKeyGenerator KeyGenerator { get; set; } = new GuidGenerator();
    /// <summary>
    ///
    /// </summary>
    public Func<Metadata, string>? NameResolver { get; set; }

    /// <summary>
    ///
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
using CoreOne.ModelPatch.Services;

namespace CoreOne.ModelPatch.Models;

public class ModelOptions
{
    public Data<Type, IEqualityComparer> Comparer { get; } = [];
    public DataHashSet<Type, string> IgnoreFields { get; }
    public IKeyGenerator KeyGenerator { get; set; } = new GuidGenerator();

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
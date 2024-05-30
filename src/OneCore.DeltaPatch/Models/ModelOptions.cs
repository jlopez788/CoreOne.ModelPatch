using OneCore.ModelPatch.Services;

namespace OneCore.ModelPatch.Models;

public class ModelOptions
{
    public Data<Type, IEqualityComparer> Comparer { get; } = [];
    public DataHashSet<Type, string> IgnoreFields { get; }
    public IKeyGenerator KeyGenerator { get; init; } = new IdGuidGenerator();

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
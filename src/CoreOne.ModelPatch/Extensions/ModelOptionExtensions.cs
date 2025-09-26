using System.Collections.Concurrent;

namespace CoreOne.ModelPatch.Extensions;

internal static class ModelOptionExtensions
{
    private static readonly ConcurrentDictionary<Metadata, string> Names = new();

    public static string GetPreferredName(this ModelOptions options, Metadata meta)
    {
        return Names.GetOrAdd(meta, p => options.NameResolver?.Invoke(p) ?? p.Name);
    }
}
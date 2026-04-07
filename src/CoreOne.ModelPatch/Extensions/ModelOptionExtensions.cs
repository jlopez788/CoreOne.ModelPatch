namespace CoreOne.ModelPatch.Extensions;

internal static class ModelOptionExtensions
{
    public static string GetPreferredName(this ModelOptions options, Metadata meta)
    {
        return options.NameResolver?.Invoke(meta) ?? meta.Name;
    }
}
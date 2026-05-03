using CoreOne.ModelPatch.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoreOne.ModelPatch.Extensions;

internal static class ModelOptionExtensions
{
    public static string GetPreferredName(this ModelOptions options, Metadata meta)
    {
        return options.NameResolver?.Invoke(meta) ?? meta.Name;
    }

    /// <summary>
    /// Registers a per-type key generator that takes precedence over the default <see cref="ModelOptions.KeyGenerator"/>.
    /// </summary>
    public static ModelOptions AddKeyGenerator<TKey>(this ModelOptions options, IKeyGenerator<TKey> generator) where TKey : notnull
    {
        options.KeyGenerators[typeof(TKey)] = generator;
        return options;
    }

    /// <summary>
    /// Resolves the best key generator for <paramref name="keyType"/> using the following order:
    /// <list type="number">
    ///   <item><see cref="ModelOptions.KeyGenerators"/> registry (explicit per-type)</item>
    ///   <item>DI open-generic <c>IKeyGenerator&lt;TKey&gt;</c></item>
    ///   <item><see cref="ModelOptions.KeyGenerator"/> fallback</item>
    /// </list>
    /// </summary>
    internal static IKeyGenerator GetKeyGenerator(this ModelOptions options, IServiceProvider services, Type keyType)
    {
        if (options.KeyGenerators.TryGetValue(keyType, out var registered))
            return registered;

        var genericType = typeof(IKeyGenerator<>).MakeGenericType(keyType);
        return services.GetService(genericType) is IKeyGenerator service ? service :
            throw new InvalidOperationException($"Key generator is not currently registered for type {keyType.Name}");
    }
}
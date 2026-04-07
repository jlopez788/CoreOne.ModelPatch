using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace CoreOne.ModelPatch.Extensions;

/// <summary>
/// Helpers for configuring common JSON property naming strategies
/// </summary>
public static class ModelOptionsJsonExtensions
{
    /// <summary>
    /// Resolves model property names using <see cref="JsonPropertyAttribute"/> values when present
    /// </summary>
    /// <param name="options">Model patch options to update</param>
    /// <returns>The same options instance for chaining</returns>
    public static ModelOptions UseNewtonsoftJsonPropertyNames(this ModelOptions options)
    {
        return options.UseNameResolver((meta, fallback) => meta.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? fallback(meta));
    }

    /// <summary>
    /// Resolves model property names using <see cref="JsonPropertyNameAttribute"/> values when present
    /// </summary>
    /// <param name="options">Model patch options to update</param>
    /// <returns>The same options instance for chaining</returns>
    public static ModelOptions UseSystemTextJsonPropertyNames(this ModelOptions options)
    {
        return options.UseNameResolver((meta, fallback) => meta.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? fallback(meta));
    }

    /// <summary>
    /// Resolves model property names using either Newtonsoft.Json or System.Text.Json naming attributes
    /// </summary>
    /// <param name="options">Model patch options to update</param>
    /// <returns>The same options instance for chaining</returns>
    public static ModelOptions UseJsonPropertyNames(this ModelOptions options)
    {
        return options.UseNameResolver((meta, fallback) =>
            meta.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName
            ?? meta.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? fallback(meta));
    }

    private static ModelOptions UseNameResolver(this ModelOptions options, Func<Metadata, Func<Metadata, string>, string> resolver)
    {
        var previous = options.NameResolver;
        options.NameResolver = meta => resolver(meta, p => previous?.Invoke(p) ?? p.Name);
        return options;
    }
}
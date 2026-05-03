using CoreOne.ModelPatch.Services;
using Microsoft.Extensions.Options;

namespace CoreOne.ModelPatch.Plugins;

/// <summary>
/// Validates optimistic concurrency tokens for updates.
/// </summary>
public class ConcurrencyTokenValidationPlugin(IOptions<ModelOptions> options) : IPrePatchPlugin
{
    private readonly ModelOptions _options = options.Value ?? new();
    public int Order => 800;

    public ValueTask<IResult> Execute(ModelProcessContext context, CancellationToken cancellationToken = default)
    {
        var modelContext = context.Context;
        if (modelContext.ConcurrencyTokens.Count == 0)
            return ValueTask.FromResult(Result.Ok);

        var providedTokens = modelContext.ConcurrencyTokens
            .Where(token => context.Delta.ContainsKey(_options.GetPreferredName(token)) || context.Delta.ContainsKey(token.Name))
            .ToList();

        if (_options.RequireConcurrencyTokenForUpdates && providedTokens.Count == 0)
            return ValueTask.FromResult(Result.Fail($"Concurrency token is required for updates to {context.Type.Name}"));

        if (!_options.ValidateConcurrencyTokens || providedTokens.Count == 0)
            return ValueTask.FromResult(Result.Ok);

        foreach (var token in providedTokens)
        {
            var fieldName = _options.GetPreferredName(token);
            var rawValue = context.Delta.TryGetValue(fieldName, out object? tokenValue) ? tokenValue : context.Delta[token.Name];
            var expected = token.GetValue(context.Model);
            var incoming = ConvertTokenValue(token.FPType, rawValue);
            if (!TokenEquals(token.FPType, expected, incoming))
                return ValueTask.FromResult(Result.Fail($"Concurrency token mismatch for {context.Type.Name}.{token.Name}"));
        }

        return ValueTask.FromResult(Result.Ok);
    }

    private static object? ConvertTokenValue(Type type, object? value)
    {
        if (value is null)
            return null;

        if (type == typeof(byte[]))
        {
            return value switch {
                byte[] bytes => bytes,
                IEnumerable<byte> sequence => sequence.ToArray(),
                string encoded => Convert.FromBase64String(encoded),
                _ => value
            };
        }

        var parsed = Types.Parse(type, value);
        return parsed.Success ? parsed.Model : value;
    }

    private static bool TokenEquals(Type type, object? left, object? right)
    {
        return (left is null && right is null) || (left is not null && right is not null && (type == typeof(byte[]))
            ? ((byte[])left).SequenceEqual((byte[])right)
            : left?.Equals(right) == true);
    }
}
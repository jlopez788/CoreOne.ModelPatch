using CoreOne.ModelPatch.Services;
using Microsoft.Extensions.Options;

namespace CoreOne.ModelPatch.Plugins;

/// <summary>
/// Validates delta keys against the target entity's allowed properties.
/// </summary>
public class StrictPropertyValidationPlugin(IOptions<ModelOptions> options) : IPrePatchPlugin
{
    private readonly ModelOptions _options = options.Value ?? new() { KeyGenerator = new GuidGenerator() };
    public int Order => 1001;

    public ValueTask<IResult> Execute(ModelProcessContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.StrictPropertyMatching || context.Delta.Count == 0)
            return ValueTask.FromResult(Result.Ok);

        var modelContext = context.Context;
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var metadata in modelContext.Properties.Values)
        {
            allowed.Add(metadata.Name);
            allowed.Add(_options.GetPreferredName(metadata));
        }

        var unknown = context.Delta.Keys
            .Where(key => !allowed.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return unknown.Count == 0 ?
            ValueTask.FromResult(Result.Ok) :
            ValueTask.FromResult(Result.Fail($"Unknown fields for {context.Type.Name}: {string.Join(", ", unknown)}"));
    }
}
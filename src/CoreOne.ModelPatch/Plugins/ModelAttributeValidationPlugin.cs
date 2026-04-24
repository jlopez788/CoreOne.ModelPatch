namespace CoreOne.ModelPatch.Plugins;

public class ModelAttributeValidationPlugin : IPrePatchPlugin
{
    public int Order => 100;
    private record Info(Metadata Property, PatchRestrictAttribute Attribute);

    public ValueTask<IResult> Execute(ModelProcessContext context, CancellationToken cancellationToken = default)
    {
        var properties = (from prop in context.Context.Properties
                          let attribute = prop.Value.GetCustomAttribute<PatchRestrictAttribute>()
                          where attribute is not null
                          select new Info(prop.Value, attribute)).ToList();

        if (properties.Count > 0)
        {
            foreach (var property in properties)
            {
                if (property.Attribute.Scope == PatchRestrictionType.DenyUpdateSilently)
                {
                    context.Delta.Remove(property.Property.Name);
                }
            }
        }

        return ValueTask.FromResult(Result.Ok);
    }
}
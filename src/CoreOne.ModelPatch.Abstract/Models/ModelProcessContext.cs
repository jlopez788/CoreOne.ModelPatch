namespace CoreOne.ModelPatch.Abstract.Models;

public class ModelProcessContext(ModelContext context, Delta delta, object model, CrudType state)
{
    public Data<string, object> AdditionalProperties { get; } = new(StringComparer.InvariantCultureIgnoreCase);
    public ModelContext Context { get; } = context;
    public Delta Delta { get; } = delta;
    public object Model { get; } = model;
    public CrudType State { get; } = state;
    public Type Type => Context.Type;
}
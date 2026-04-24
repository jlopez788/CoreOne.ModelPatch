namespace CoreOne.ModelPatch.Abstract.Models;

public class KeyModel(Type type, object model)
{
    public Type KeyType { get; } = type;
    public object Model { get; } = model;

    public static KeyModel Create<T>(T model) where T : notnull => new(typeof(T), model);

    public void Deconstruct(out Type keyType, out object model)
    {
        keyType = KeyType;
        model = Model;
    }
}
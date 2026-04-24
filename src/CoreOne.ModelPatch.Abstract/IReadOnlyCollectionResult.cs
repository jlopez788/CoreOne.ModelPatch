namespace CoreOne.ModelPatch;

public interface IReadOnlyCollectionResult<TModel> : IResult
{
    IReadOnlyCollection<TModel>? Items { get; }
}
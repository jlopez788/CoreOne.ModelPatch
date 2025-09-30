namespace CoreOne.ModelPatch.Extensions;

public static class ProcessedModelExtensions
{
    public static int Count(this IResult<ProcessedModelCollection> result, Predicate<ModelState>? predicate = null)
    {
        return result.Success && result.Model?.Count > 0 ?
            predicate is null ?
            result.Model.Count :
            result.Model.Count(p => predicate(p)) : 0;
    }

    public static IEnumerable<T> OfType<T>(this IResult<ProcessedModelCollection> result, Predicate<ModelState>? predicate = null)
    {
        return result.Success && result.Model?.Count > 0 ?
            predicate is null ?
            result.Model.Select(p => p.Model).OfType<T>() :
            result.Model.Where(p => predicate(p))
                .Select(p => p.Model)
                .OfType<T>() : [];
    }
}
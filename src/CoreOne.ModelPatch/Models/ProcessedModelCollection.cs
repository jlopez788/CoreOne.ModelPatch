namespace CoreOne.ModelPatch.Models;

/// <summary>
/// Collection of entities processed during a patch operation with CRUD operation metadata
/// </summary>
public class ProcessedModelCollection : IReadOnlyList<ModelState>
{
    private readonly List<ModelState> States = [];

    /// <summary>
    /// Total number of processed entities
    /// </summary>
    public int Count => States.Count;

    /// <summary>
    /// Accesses a processed entity by index
    /// </summary>
    public ModelState this[int index] => States[index];

    /// <summary>
    /// Enumerates all processed entities
    /// </summary>
    public IEnumerator<ModelState> GetEnumerator() => States.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => States.GetEnumerator();

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"Count: {Count}";

    internal ProcessedModelCollection Add(ModelState state)
    {
        States.Add(state);
        return this;
    }

    internal ProcessedModelCollection AddRange(ProcessedModelCollection? collection)
    {
        collection.Each(p => Add(p));
        return this;
    }
}
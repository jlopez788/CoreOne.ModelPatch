namespace CoreOne.ModelPatch.Models;

public class ProcessedModelCollection : IReadOnlyList<ModelState>
{
    private readonly List<ModelState> States = [];
    public int Count => States.Count;
    public ModelState this[int index] => States[index];

    public IEnumerator<ModelState> GetEnumerator() => States.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => States.GetEnumerator();

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
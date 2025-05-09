namespace CoreOne.ModelPatch.Models;

public readonly struct PatchResult<T>(T? model, int rows) : IResult<T>
{
    public T? Model { get; } = model;
    public int Rows { get; init; } = rows;
    public string? Message { get; init; }
    public ResultType ResultType { get; init; } = model is not null ? ResultType.Success : ResultType.Fail;
}
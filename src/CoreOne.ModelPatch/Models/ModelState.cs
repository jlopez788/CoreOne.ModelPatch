namespace CoreOne.ModelPatch.Models;

/// <summary>
///
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="Model"></param>
/// <param name="CrudType"></param>
public record ModelState<T>(T? Model, CrudType CrudType);
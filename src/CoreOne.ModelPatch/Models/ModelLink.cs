namespace CoreOne.ModelPatch.Models;

/// <summary>
///
/// </summary>
/// <param name="ParentProperties"></param>
/// <param name="ChildProperty"></param>
public record ModelLink(List<ModelKey> ParentProperties, string ChildProperty);
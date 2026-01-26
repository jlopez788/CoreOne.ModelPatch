namespace CoreOne.ModelPatch.Models;

/// <summary>
/// Represents a parent-child relationship for automatic foreign key injection
/// </summary>
/// <param name="ParentProperties">Primary key properties from the parent entity</param>
/// <param name="ChildProperty">Foreign key property name in the child entity</param>
public record ModelLink(List<ModelKey> ParentProperties, string ChildProperty);
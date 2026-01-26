namespace CoreOne.ModelPatch.Models;

/// <summary>
/// Represents a key property (primary or unique index) for entity identification
/// </summary>
/// <param name="Name">Property name of the key</param>
/// <param name="IsPrimaryKey">True if this is a primary key, false if it's a unique index</param>
public record ModelKey(string Name, bool IsPrimaryKey);

namespace CoreOne.ModelPatch.Models;

/// <summary>
/// Indicates the type of operation performed on an entity during patch processing
/// </summary>
[Flags]
public enum CrudType
{
    /// <summary>
    /// Entity was inserted into the database
    /// </summary>
    Created = 1,
    /// <summary>
    /// Entity was retrieved from the database
    /// </summary>
    Read = 2,
    /// <summary>
    /// Existing entity was modified
    /// </summary>
    Updated = 4,
    /// <summary>
    /// Entity was removed from the database
    /// </summary>
    Deleted = 8
}
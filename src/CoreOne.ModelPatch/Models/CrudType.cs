namespace CoreOne.ModelPatch.Models;

/// <summary>
/// 
/// </summary>
[Flags]
public enum CrudType
{
    /// <summary>
    /// 
    /// </summary>
    Created = 1,
    /// <summary>
    /// 
    /// </summary>
    Read = 2,
    /// <summary>
    /// 
    /// </summary>
    Updated = 4,
    /// <summary>
    /// 
    /// </summary>
    Deleted = 8
}
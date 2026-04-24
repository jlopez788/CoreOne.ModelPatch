namespace CoreOne.ModelPatch.Abstract.Models;

[Flags]
public enum PatchRestrictionType
{
    /// <summary>
    /// Process field as normal
    /// </summary>
    Default,
    /// <summary>
    /// Deny update to the field, but continues to process the rest of the patch request as normal. The field will be ignored and left unchanged.
    /// </summary>
    DenyUpdateSilently,
    /// <summary>
    /// Deny update to the field and return a bad request response.
    /// </summary>
    DenyUpdateBadRequest,
}
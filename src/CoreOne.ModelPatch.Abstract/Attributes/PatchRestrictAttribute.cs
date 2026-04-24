namespace CoreOne.ModelPatch.Abstract.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class PatchRestrictAttribute(PatchRestrictionType scope = PatchRestrictionType.Default) : Attribute
{
    public PatchRestrictionType Scope { get; set; } = scope;
}
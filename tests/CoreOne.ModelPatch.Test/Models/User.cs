using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreOne.ModelPatch.Test.Models;

[Table("user")]
public class User
{
    [Key] public Guid Id { get; set; }
    public string? Email { get; set; }
    public bool IsLocked { get; set; }
    public UserStatus Status { get; set; } = UserStatus.New;
}

public enum UserStatus
{
    New,
    Approved
}
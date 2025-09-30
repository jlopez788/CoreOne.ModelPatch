using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CoreOne.ModelPatch.Test.Models;

[Index(nameof(IsActive), IsUnique = false)]
public class Model
{
    [Key]
    public Guid Key { get; set; } = ID.CreateV7();
    public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedOnUtc { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
using System.ComponentModel.DataAnnotations;

namespace CoreOne.ModelPatch.Test.Models;

public class VersionedBlog
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
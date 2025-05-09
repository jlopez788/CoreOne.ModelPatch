using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreOne.ModelPatch.Test.Models;

public class Blog
{
    [Key] public Guid BlogId { get; set; }
    [Required, StringLength(10)] public string Name { get; set; } = default!;
    [StringLength(20)] public string? Url { get; set; }

    [InverseProperty(nameof(Post.Blog))]
    public List<Post> Posts { get; init; } = [];
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreOne.ModelPatch.Test.Models;

public class Post
{
    [Key] public Guid PostId { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }

    public Guid MyBlogId { get; set; }
    [ForeignKey(nameof(MyBlogId))]
    public Blog? Blog { get; set; }
}
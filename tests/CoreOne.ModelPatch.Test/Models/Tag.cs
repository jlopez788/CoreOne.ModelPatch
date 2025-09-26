using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreOne.ModelPatch.Test.Models;

[Table("Tag")]
[Index(nameof(Name), IsUnique = true)]
public class Tag
{
    [Key] public Guid Id { get; set; }
    [JsonProperty("name_one")]
    public string Name { get; set; } = string.Empty;

    public Guid? MyBlogId { get; set; }
    [ForeignKey(nameof(MyBlogId))]
    public Blog? Blog { get; set; }

    public Tag()
    { }

    public Tag(string name)
    {
        Name = name;
    }
}
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreOne.ModelPatch.Test.Models;

[Table("ChatMessage")]
[Index(nameof(CreatedOnUtc))]
public class ChatMessage : Model
{
    [Required]
    [JsonProperty("session_key")]
    public Guid SessionKey { get; set; }
    [Required]
    public ChatRoleType Role { get; set; }
    [Required]
    public string? Content { get; set; }

    [ForeignKey(nameof(SessionKey))]
    public virtual ChatSession? Session { get; set; }

    public ChatMessage()
    { }

    public ChatMessage(ChatRoleType role, string? content = null)
    {
        Role = role;
        Content = content;
    }
}
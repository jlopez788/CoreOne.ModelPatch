using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreOne.ModelPatch.Test.Models;

[Table("ChatSession")]
[Index(nameof(Title), AllDescending = true)]
public class ChatSession : Model
{
    [Required]
    [MaxLength(200)]
    [JsonProperty("title_here")]
    public string Title { get; set; } = "New Chat";

    [JsonProperty("msgs")]
    [InverseProperty(nameof(ChatMessage.Session))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public ICollection<ChatMessage> Messages { get; set; } = [];

    public ChatSession()
    { }

    public ChatSession(string title)
    {
        Title = title;
    }
}
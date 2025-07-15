namespace MCP.Core.Models;

public class CommentEntry
{
    public string Author { get; set; }              // "AI-Agent", "User", etc.
    public string Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string RelatedToNodeId { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

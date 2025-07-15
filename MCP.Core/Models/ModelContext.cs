namespace MCP.Core.Models;

public class ModelContext
{
    public string Type { get; set; }                // "Topology", "Code", "Thread"
    public string Content { get; set; }             // Raw text or HTML
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
namespace MCP.Core.Models;

public class IssueNode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Status { get; set; }              // "Healthy", "Error", "Degraded"
    public string LinkedCodePath { get; set; }      // GitHub or local path
    public List<string> Tags { get; set; } = new();
}

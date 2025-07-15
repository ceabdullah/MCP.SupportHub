using MCP.Core.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");


app.MapGet("/api/topology/nodes", () =>
{
    return new List<IssueNode>
    {
        new IssueNode { Id = "node-01", Name = "Auth Gateway", Status = "Healthy", LinkedCodePath = "/auth/gateway.cs" },
        new IssueNode { Id = "node-02", Name = "Database Layer", Status = "Error", LinkedCodePath = "/data/dbcontext.cs" },
        new IssueNode { Id = "node-03", Name = "Load Balancer", Status = "Degraded", LinkedCodePath = "/infra/loadbalancer.json" }
    };
});

app.MapGet("/api/comments/{nodeId}", (string nodeId) =>
{
    return new List<CommentEntry>
    {
        new CommentEntry { Author = "AI-Agent", Message = $"Node {nodeId} might be misconfigured", Timestamp = DateTime.UtcNow },
        new CommentEntry { Author = "User", Message = $"Looking into load balancer routing issues.", Timestamp = DateTime.UtcNow }
    };
});

app.MapGet("/api/code/{nodeId}", (string nodeId) =>
{
    var code = nodeId switch
    {
        "node-01" => @"public class AuthGateway { public void Authenticate() => true; }",
        "node-02" => @"public class DbContext { public void Connect() { /* ... */ } }",
        _ => "// No code found"
    };
    return code;
});

var commentsStore = new Dictionary<string, List<CommentEntry>>();

app.MapPost("/api/comments/{nodeId}", async (string nodeId, CommentEntry comment) =>
{
    if (!commentsStore.ContainsKey(nodeId))
        commentsStore[nodeId] = new List<CommentEntry>();

    comment.Timestamp = DateTime.UtcNow;
    commentsStore[nodeId].Add(comment);

    return Results.Ok();
});

app.MapPost("/api/diagnostics", async (IssueNode node, HttpClient http) =>
{
    var payload = new
    {
        system = "You're a diagnostic assistant for infrastructure.",
        prompt = $"Analyze node '{node.Name}' with status '{node.Status}'. Code path: {node.LinkedCodePath}."
    };

    var response = await http.PostAsJsonAsync("http://localhost:5000/llm/infer", payload);
    var data = await response.Content.ReadFromJsonAsync<LLMResponse>();

    return new ModelContext
    {
        Type = "CustomAgent",
        Content = data?.result ?? "[No response]",
        Metadata = new Dictionary<string, object>
        {
            { "NodeId", node.Id },
            { "Severity", node.Status },
            { "LinkedCodePath", node.LinkedCodePath }
        }
    };
});

app.Run();

record LLMResponse(string result);

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

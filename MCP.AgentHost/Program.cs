using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// In-memory log store
var logs = new List<PromptLog>();

// Main inference endpoint
app.MapPost("/llm/infer", (LLMRequest request) =>
{
    var lastWord = request.prompt?.Split(' ').LastOrDefault() ?? "unknown";
    var responseText = $"[Custom Agent] Diagnosing: '{request.prompt}' → Action: Inspect '{lastWord}'.";

    logs.Add(new PromptLog(DateTime.UtcNow, request.system, request.prompt, responseText));

    return Results.Ok(new LLMResponse(responseText));
});

// Optional: View recent logs
app.MapGet("/llm/logs", () => Results.Ok(logs));

app.Run();

// 💾 Records
record LLMRequest(string system, string prompt);
record LLMResponse(string result);
record PromptLog(DateTime Timestamp, string SystemPrompt, string UserPrompt, string Response);

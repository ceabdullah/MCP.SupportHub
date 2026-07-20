namespace MCP.Itinerary.Models;

public class ItineraryDay
{
    public int DayNumber { get; set; }
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = [];
}

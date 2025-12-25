using System.Text.Json.Serialization;

namespace GameSwap.Functions.Models;

public sealed record Event
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("divisionId")]
    public string DivisionId { get; init; } = string.Empty;

    [JsonPropertyName("teamId")]
    public string TeamId { get; init; } = string.Empty;

    [JsonPropertyName("opponentTeamId")]
    public string? OpponentTeamId { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("eventDate")]
    public DateOnly EventDate { get; init; }

    [JsonPropertyName("startTime")]
    public TimeOnly StartTime { get; init; }

    [JsonPropertyName("endTime")]
    public TimeOnly EndTime { get; init; }

    [JsonPropertyName("location")]
    public string Location { get; init; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("createdByUserId")]
    public string CreatedByUserId { get; init; } = string.Empty;

    [JsonPropertyName("acceptedByUserId")]
    public string? AcceptedByUserId { get; init; }

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; init; }

    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; init; }
}

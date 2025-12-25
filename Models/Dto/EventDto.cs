using System.Text.Json.Serialization;

namespace GameSwap.Functions.Models.Dto;

public sealed record EventDto
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
    public string EventDate { get; init; } = string.Empty;

    [JsonPropertyName("startTime")]
    public string StartTime { get; init; } = string.Empty;

    [JsonPropertyName("endTime")]
    public string EndTime { get; init; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; init; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("createdByUserId")]
    public string CreatedByUserId { get; init; } = string.Empty;

    [JsonPropertyName("acceptedByUserId")]
    public string? AcceptedByUserId { get; init; }

    [JsonPropertyName("createdUtc")]
    public string CreatedUtc { get; init; } = string.Empty;

    [JsonPropertyName("updatedUtc")]
    public string UpdatedUtc { get; init; } = string.Empty;
}

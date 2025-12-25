using System.Text.Json.Serialization;

namespace GameSwap.Functions.Models.Dto;

public sealed record OpportunityDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("eventId")]
    public string EventId { get; init; } = string.Empty;

    [JsonPropertyName("teamId")]
    public string TeamId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("requestedByUserId")]
    public string RequestedByUserId { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("createdUtc")]
    public string CreatedUtc { get; init; } = string.Empty;

    [JsonPropertyName("updatedUtc")]
    public string UpdatedUtc { get; init; } = string.Empty;
}

using System.Text.Json.Serialization;

namespace GameSwap.Functions.Models;

public sealed record Opportunity
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
    public DateTimeOffset CreatedUtc { get; init; }

    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; init; }
}

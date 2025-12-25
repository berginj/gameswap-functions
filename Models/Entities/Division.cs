using System.Text.Json.Serialization;

namespace GameSwap.Functions.Models;

public sealed record Division
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("leagueId")]
    public string LeagueId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("ageGroup")]
    public string? AgeGroup { get; init; }

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; init; }

    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; init; }
}

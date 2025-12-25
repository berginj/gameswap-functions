using System.Text.Json.Serialization;

namespace GameSwap.Functions.Models.Dto;

public sealed record DivisionDto
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
    public string CreatedUtc { get; init; } = string.Empty;

    [JsonPropertyName("updatedUtc")]
    public string UpdatedUtc { get; init; } = string.Empty;
}

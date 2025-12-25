using System.Text.Json.Serialization;

namespace GameSwap.Functions.Models.Dto;

public sealed record TeamDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("leagueId")]
    public string LeagueId { get; init; } = string.Empty;

    [JsonPropertyName("divisionId")]
    public string DivisionId { get; init; } = string.Empty;

    [JsonPropertyName("coachUserId")]
    public string? CoachUserId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("createdUtc")]
    public string CreatedUtc { get; init; } = string.Empty;

    [JsonPropertyName("updatedUtc")]
    public string UpdatedUtc { get; init; } = string.Empty;
}

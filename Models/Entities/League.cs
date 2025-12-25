using System.Text.Json.Serialization;

namespace GameSwap.Functions.Models;

public sealed record League
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("organization")]
    public string? Organization { get; init; }

    [JsonPropertyName("season")]
    public string? Season { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; init; }

    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; init; }
}

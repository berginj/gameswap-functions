using Azure;
using Azure.Data.Tables;

public class TeamEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // TEAM#{leagueId}#{division}
    public string RowKey { get; set; } = default!;       // teamId

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string LeagueId { get; set; } = default!;
    public string Division { get; set; } = default!;
    public string TeamId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string PrimaryContactName { get; set; } = default!;
    public string PrimaryContactEmail { get; set; } = default!;
    public string PrimaryContactPhone { get; set; } = default!;
    public DateTimeOffset? CreatedUtc { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}

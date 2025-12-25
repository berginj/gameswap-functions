using Azure;
using Azure.Data.Tables;

public class LeagueEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // LEAGUE
    public string RowKey { get; set; } = default!;       // leagueId

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string LeagueId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Timezone { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string ContactName { get; set; } = default!;
    public string ContactEmail { get; set; } = default!;
    public string ContactPhone { get; set; } = default!;
    public DateTimeOffset? CreatedUtc { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}

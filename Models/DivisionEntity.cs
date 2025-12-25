using Azure;
using Azure.Data.Tables;

public class DivisionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // DIV#{leagueId}
    public string RowKey { get; set; } = default!;       // code

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string LeagueId { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? CreatedUtc { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}

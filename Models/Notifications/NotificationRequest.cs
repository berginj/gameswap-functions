using System.Collections.Generic;

namespace GameSwap.Functions.Models.Notifications;

public static class NotificationEventTypes
{
    public const string EventCreated = "EventCreated";
    public const string OpponentAccepted = "OpponentAccepted";
    public const string ScheduleChanged = "ScheduleChanged";
}

public sealed record NotificationRequest(
    string EventType,
    string LeagueId,
    string? EventId,
    string? SlotId,
    string? RequestId,
    string? Division,
    string? TeamId,
    string? OpponentTeamId,
    DateTimeOffset CreatedUtc,
    IReadOnlyDictionary<string, string>? Metadata = null
);

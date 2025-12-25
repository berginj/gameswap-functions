namespace GameSwap.Functions.Models;

public record CalendarEventDto(
    string eventId,
    string type,
    string status,
    string division,
    string teamId,
    string opponentTeamId,
    string title,
    string eventDate,
    string startTime,
    string endTime,
    string location,
    string sport,
    string skill,
    string notes,
    string createdByUserId,
    string acceptedByUserId,
    string createdUtc,
    string updatedUtc
);

public record SlotOpportunityDto(
    string slotId,
    string leagueId,
    string division,
    string offeringTeamId,
    string confirmedTeamId,
    string gameDate,
    string startTime,
    string endTime,
    string parkName,
    string fieldName,
    string displayName,
    string fieldKey,
    string gameType,
    string status,
    string sport,
    string skill,
    string notes
);

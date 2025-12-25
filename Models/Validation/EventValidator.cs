using GameSwap.Functions.Models.Dto;

namespace GameSwap.Functions.Models.Validation;

public static class EventValidator
{
    public static ValidationResult Validate(Event model)
    {
        var result = new ValidationResult();
        result.Required(model.Id, "id");
        result.Required(model.Type, "type");
        result.Required(model.Status, "status");
        result.Required(model.DivisionId, "divisionId");
        result.Required(model.TeamId, "teamId");
        result.Required(model.Title, "title");
        result.Required(model.EventDate, "eventDate");
        result.Required(model.StartTime, "startTime");
        result.Required(model.EndTime, "endTime");
        result.Required(model.Location, "location");
        result.Required(model.CreatedByUserId, "createdByUserId");
        result.Required(model.CreatedUtc, "createdUtc");
        result.Required(model.UpdatedUtc, "updatedUtc");
        return result;
    }

    public static ValidationResult Validate(EventDto dto)
    {
        var result = new ValidationResult();
        result.Required(dto.Id, "id");
        result.Required(dto.Type, "type");
        result.Required(dto.Status, "status");
        result.Required(dto.DivisionId, "divisionId");
        result.Required(dto.TeamId, "teamId");
        result.Required(dto.Title, "title");
        result.Required(dto.EventDate, "eventDate");
        result.Required(dto.StartTime, "startTime");
        result.Required(dto.EndTime, "endTime");
        result.Required(dto.Location, "location");
        result.Required(dto.CreatedByUserId, "createdByUserId");
        result.Required(dto.CreatedUtc, "createdUtc");
        result.Required(dto.UpdatedUtc, "updatedUtc");
        return result;
    }
}

using GameSwap.Functions.Models.Dto;

namespace GameSwap.Functions.Models.Validation;

public static class OpportunityValidator
{
    public static ValidationResult Validate(Opportunity model)
    {
        var result = new ValidationResult();
        result.Required(model.Id, "id");
        result.Required(model.EventId, "eventId");
        result.Required(model.TeamId, "teamId");
        result.Required(model.Status, "status");
        result.Required(model.RequestedByUserId, "requestedByUserId");
        result.Required(model.CreatedUtc, "createdUtc");
        result.Required(model.UpdatedUtc, "updatedUtc");
        return result;
    }

    public static ValidationResult Validate(OpportunityDto dto)
    {
        var result = new ValidationResult();
        result.Required(dto.Id, "id");
        result.Required(dto.EventId, "eventId");
        result.Required(dto.TeamId, "teamId");
        result.Required(dto.Status, "status");
        result.Required(dto.RequestedByUserId, "requestedByUserId");
        result.Required(dto.CreatedUtc, "createdUtc");
        result.Required(dto.UpdatedUtc, "updatedUtc");
        return result;
    }
}

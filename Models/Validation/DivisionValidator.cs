using GameSwap.Functions.Models.Dto;

namespace GameSwap.Functions.Models.Validation;

public static class DivisionValidator
{
    public static ValidationResult Validate(Division model)
    {
        var result = new ValidationResult();
        result.Required(model.Id, "id");
        result.Required(model.LeagueId, "leagueId");
        result.Required(model.Name, "name");
        result.Required(model.CreatedUtc, "createdUtc");
        result.Required(model.UpdatedUtc, "updatedUtc");
        return result;
    }

    public static ValidationResult Validate(DivisionDto dto)
    {
        var result = new ValidationResult();
        result.Required(dto.Id, "id");
        result.Required(dto.LeagueId, "leagueId");
        result.Required(dto.Name, "name");
        result.Required(dto.CreatedUtc, "createdUtc");
        result.Required(dto.UpdatedUtc, "updatedUtc");
        return result;
    }
}

using GameSwap.Functions.Models.Dto;

namespace GameSwap.Functions.Models.Validation;

public static class LeagueValidator
{
    public static ValidationResult Validate(League model)
    {
        var result = new ValidationResult();
        result.Required(model.Id, "id");
        result.Required(model.Name, "name");
        result.Required(model.Status, "status");
        result.Required(model.CreatedUtc, "createdUtc");
        result.Required(model.UpdatedUtc, "updatedUtc");
        return result;
    }

    public static ValidationResult Validate(LeagueDto dto)
    {
        var result = new ValidationResult();
        result.Required(dto.Id, "id");
        result.Required(dto.Name, "name");
        result.Required(dto.Status, "status");
        result.Required(dto.CreatedUtc, "createdUtc");
        result.Required(dto.UpdatedUtc, "updatedUtc");
        return result;
    }
}

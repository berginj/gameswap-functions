using GameSwap.Functions.Models.Dto;

namespace GameSwap.Functions.Models.Validation;

public static class FieldValidator
{
    public static ValidationResult Validate(Field model)
    {
        var result = new ValidationResult();
        result.Required(model.Id, "id");
        result.Required(model.Name, "name");
        result.Required(model.Address, "address");
        result.Required(model.City, "city");
        result.Required(model.State, "state");
        result.Required(model.PostalCode, "postalCode");
        result.Required(model.TimeZone, "timeZone");
        result.Required(model.CreatedUtc, "createdUtc");
        result.Required(model.UpdatedUtc, "updatedUtc");
        return result;
    }

    public static ValidationResult Validate(FieldDto dto)
    {
        var result = new ValidationResult();
        result.Required(dto.Id, "id");
        result.Required(dto.Name, "name");
        result.Required(dto.Address, "address");
        result.Required(dto.City, "city");
        result.Required(dto.State, "state");
        result.Required(dto.PostalCode, "postalCode");
        result.Required(dto.TimeZone, "timeZone");
        result.Required(dto.CreatedUtc, "createdUtc");
        result.Required(dto.UpdatedUtc, "updatedUtc");
        return result;
    }
}

using GameSwap.Functions.Models.Dto;

namespace GameSwap.Functions.Models.Validation;

public static class UserValidator
{
    public static ValidationResult Validate(User model)
    {
        var result = new ValidationResult();
        result.Required(model.Id, "id");
        result.Required(model.Email, "email");
        result.Required(model.DisplayName, "displayName");
        result.Required(model.Role, "role");
        result.Required(model.Status, "status");
        result.Required(model.CreatedUtc, "createdUtc");
        result.Required(model.UpdatedUtc, "updatedUtc");
        return result;
    }

    public static ValidationResult Validate(UserDto dto)
    {
        var result = new ValidationResult();
        result.Required(dto.Id, "id");
        result.Required(dto.Email, "email");
        result.Required(dto.DisplayName, "displayName");
        result.Required(dto.Role, "role");
        result.Required(dto.Status, "status");
        result.Required(dto.CreatedUtc, "createdUtc");
        result.Required(dto.UpdatedUtc, "updatedUtc");
        return result;
    }
}

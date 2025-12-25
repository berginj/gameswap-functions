using GameSwap.Functions.Storage;
using Xunit;

namespace GameSwap.Functions.Tests.Validation;

public class FieldImportValidationTests
{
    [Theory]
    [InlineData("Park/Field", "park", "field")]
    [InlineData("Park_Field", "park", "field")]
    public void Parses_field_key_formats(string raw, string expectedPark, string expectedField)
    {
        var ok = FieldImportValidation.TryParseFieldKeyFlexible(raw, "Park", "Field", out var parkCode, out var fieldCode, out var normalized);

        Assert.True(ok);
        Assert.Equal(expectedPark, parkCode);
        Assert.Equal(expectedField, fieldCode);
        Assert.Equal($"{expectedPark}/{expectedField}", normalized);
    }

    [Fact]
    public void Parses_is_active_from_status()
    {
        var active = FieldImportValidation.ParseIsActive("Active", "");
        var inactive = FieldImportValidation.ParseIsActive("Inactive", "");

        Assert.True(active);
        Assert.False(inactive);
    }
}

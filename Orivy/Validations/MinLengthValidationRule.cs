using Orivy.Controls;

namespace Orivy.Validations;

public class MinLengthValidationRule : ValidationRule
{
    public int MinLength { get; set; }

    public override bool Validate(ElementBase element, out string errorMessage)
    {
        if (element.Text.Length < MinLength)
        {
            errorMessage = ErrorMessage ?? $"This field must be at least {MinLength} characters.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
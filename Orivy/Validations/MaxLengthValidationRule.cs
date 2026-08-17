using Orivy.Controls;

namespace Orivy.Validations;

public class MaxLengthValidationRule : ValidationRule
{
    public int MaxLength { get; set; }

    public override bool Validate(ElementBase element, out string errorMessage)
    {
        if (element.Text.Length > MaxLength)
        {
            errorMessage = ErrorMessage ?? $"This field must be at most {MaxLength} characters.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
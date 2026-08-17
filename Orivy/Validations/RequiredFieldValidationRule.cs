using Orivy.Controls;

namespace Orivy.Validations;

public class RequiredFieldValidationRule : ValidationRule
{
    public override bool Validate(ElementBase element, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(element.Text))
        {
            errorMessage = ErrorMessage ?? "This field cannot be left empty.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
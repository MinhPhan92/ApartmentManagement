using System.ComponentModel.DataAnnotations;

namespace ApartmentManagement.Common.Validation;

public static class ResidentValidation
{
    public const int FullNameMaxLength = 200;
    public const int CitizenIdMaxLength = 20;
    public const int GenderMaxLength = 20;
    public const int AddressMaxLength = 255;
    public const int PhoneMaxLength = 50;
    public const int RelationshipMaxLength = 100;
    public const string CitizenIdPattern = @"^(?:\d{9}|\d{12})$";
    public const string PhonePattern = @"^\+?[0-9]{9,15}$";
}

public sealed class ValidDateOfBirthAttribute : ValidationAttribute
{
    public ValidDateOfBirthAttribute() => ErrorMessage = "Ngày sinh phải từ 01/01/1900 đến ngày hiện tại.";
    public override bool IsValid(object? value) => value is null ||
        value is DateTime date && date.Date >= new DateTime(1900, 1, 1) && date.Date <= DateTime.UtcNow.Date;
}

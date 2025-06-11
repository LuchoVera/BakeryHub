using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;

public class UpdateUserProfileDto
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "The name must be between 2 and 150 characters long.")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "Phone number is required.")]
    [StringLength(8, MinimumLength = 8, ErrorMessage = "The phone number must be 8 digits long.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "The phone number must contain exactly 8 digits.")]
    public string? PhoneNumber { get; set; }
}

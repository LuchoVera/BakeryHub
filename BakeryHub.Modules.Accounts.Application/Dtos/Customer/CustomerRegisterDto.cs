using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Modules.Accounts.Application.Dtos.Customer;

public class CustomerRegisterDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public required string Name { get; set; }

    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    public required string Password { get; set; }

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public required string ConfirmPassword { get; set; }

    [Required]
    [StringLength(8, MinimumLength = 8, ErrorMessage = "Phone number must be 8 digits.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "Phone number must contain exactly 8 digits.")]
    public required string PhoneNumber { get; set; }
}

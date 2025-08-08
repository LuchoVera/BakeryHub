using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Modules.Accounts.Application.Dtos.Admin;

public class AdminRegisterDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public required string AdminName { get; set; }

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

    [Required]
    [StringLength(50, MinimumLength = 3)]
    public required string BusinessName { get; set; }

    [Required]
    [RegularExpression(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Subdomain must be lowercase alphanumeric with optional hyphens.")]
    [StringLength(30, MinimumLength = 3)]
    public required string Subdomain { get; set; }
}

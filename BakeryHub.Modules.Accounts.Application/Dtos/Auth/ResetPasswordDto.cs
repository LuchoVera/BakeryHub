using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Modules.Accounts.Application.Dtos.Auth;

public class ResetPasswordDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    public required string Token { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    public required string NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The password and confirmation password do not match.")]
    public required string ConfirmNewPassword { get; set; }
}

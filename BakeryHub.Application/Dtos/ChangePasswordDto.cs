using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;

public class ChangePasswordDto
{
    [Required(ErrorMessage = "Current password is required.")]
    [DataType(DataType.Password)]
    public required string CurrentPassword { get; set; }

    [Required(ErrorMessage = "New password is required.")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "The password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    public required string NewPassword { get; set; }

    [Required(ErrorMessage = "Confirm new password is required.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation password do not match.")]
    public required string ConfirmNewPassword { get; set; }
}

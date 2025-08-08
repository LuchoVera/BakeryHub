using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Modules.Accounts.Application.Dtos.Auth;


public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
}

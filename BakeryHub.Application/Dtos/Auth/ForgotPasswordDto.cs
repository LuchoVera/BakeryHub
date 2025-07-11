using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
}

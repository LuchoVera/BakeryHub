using System.ComponentModel.DataAnnotations;
namespace BakeryHub.Application.Dtos.Admin;

public class UpdateAdminProfileDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public required string AdminName { get; set; }

    [Required]
    [StringLength(8, MinimumLength = 8, ErrorMessage = "Phone number must be 8 digits.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "Phone number must contain exactly 8 digits.")]
    public required string PhoneNumber { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public required string BusinessName { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Application.Dtos;
public class LinkCustomerDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
}

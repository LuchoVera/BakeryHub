using System.ComponentModel.DataAnnotations;

namespace BakeryHub.Modules.Accounts.Application.Dtos.Customer;
public class LinkCustomerDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
}

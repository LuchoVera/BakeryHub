using BakeryHub.Modules.Accounts.Application.Dtos.Enums;
using Microsoft.AspNetCore.Identity;

namespace BakeryHub.Modules.Accounts.Application.Dtos.Customer;

public class DetailedRegistrationResult
{
    public IdentityResult IdentityResult { get; set; } = IdentityResult.Failed();
    public Guid? UserId { get; set; }
    public RegistrationOutcome Outcome { get; set; } = RegistrationOutcome.Failed;
}

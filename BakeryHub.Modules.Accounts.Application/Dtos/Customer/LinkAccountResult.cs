using BakeryHub.Modules.Accounts.Application.Dtos.Enums;
using Microsoft.AspNetCore.Identity;

namespace BakeryHub.Modules.Accounts.Application.Dtos.Customer;

public class LinkAccountResult
{
    public IdentityResult IdentityResult { get; set; } = IdentityResult.Failed();
    public LinkAccountOutcome Outcome { get; set; } = LinkAccountOutcome.Failed;
}

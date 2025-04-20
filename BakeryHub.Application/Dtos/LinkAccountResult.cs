using BakeryHub.Application.Dtos.Enums;
using Microsoft.AspNetCore.Identity;

namespace BakeryHub.Application.Dtos;

public class LinkAccountResult
{
    public IdentityResult IdentityResult { get; set; } = IdentityResult.Failed();
    public LinkAccountOutcome Outcome { get; set; } = LinkAccountOutcome.Failed;
}

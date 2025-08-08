using Microsoft.AspNetCore.Identity;

namespace BakeryHub.Modules.Accounts.Domain.Models;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}

namespace BakeryHub.Modules.Accounts.Application.Dtos.Enums;

public enum RegistrationOutcome
{
    Failed,
    UserCreated,
    MembershipCreated,
    AlreadyMember,
    AdminConflict,
    TenantNotFound,
    RoleAssignmentFailed,
    UnknownError
}

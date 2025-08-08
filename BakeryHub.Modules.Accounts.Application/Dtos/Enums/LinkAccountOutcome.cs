namespace BakeryHub.Modules.Accounts.Application.Dtos.Enums;

public enum LinkAccountOutcome
{
    Failed,
    Linked,
    AlreadyMember,
    UserNotFound,
    UserNotCustomer,
    AdminConflict,
    TenantNotFound,
    DbError
}

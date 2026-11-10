namespace Domain.Enums;

/// <summary>
/// The three roles of the system. Public registration only ever creates <see cref="User"/>; Admin accounts are created by a
/// SuperAdmin, and the first SuperAdmin by the server-side bootstrap. The role is stored as its name.
/// </summary>
public enum UserRole
{
    User,
    Admin,
    SuperAdmin
}

/// <summary>The role names as they appear in tokens and policies; use these instead of writing the words again.</summary>
public static class RoleNames
{
    public const string User = nameof(UserRole.User);
    public const string Admin = nameof(UserRole.Admin);
    public const string SuperAdmin = nameof(UserRole.SuperAdmin);
}

namespace App.Features.Identity;

public sealed record UserOutput(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool EmailConfirmed,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

internal static class UserMapper
{
    public static UserOutput ToOutput(this User user) =>
        new(
            user.Id,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            user.EmailConfirmed,
            user.CreatedAt,
            user.LastLoginAt);
}

using InkWell.Shared;

namespace InkWellAuth.API.Models;

// three roles for now - reader is the default when someone signs up
public enum UserRole { READER, AUTHOR, ADMIN }

public class User : BaseEntity
{
    // separate from BaseEntity.Id because we expose UserId in api responses
    // keeps things clear at the boundary
    public Guid UserId { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = string.Empty;

    // always stored lowercase to avoid duplicate email issues
    public string Email { get; set; } = string.Empty;

    // bcrypt hash, never store plain text
    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    // defaults to reader on signup
    public UserRole Role { get; set; } = UserRole.READER;

    // optional profile fields
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }

    // local = password login, could be google/github later
    public string Provider { get; set; } = "local";

    // soft deactivation - user still exists in db but cant login
    public bool IsActive { get; set; } = true;

    // stored refresh token for token rotation
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
}

namespace InkWellAuth.API.DTOs;

// Requests

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string FullName
);

public record LoginRequest(
    string Email,
    string Password
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record UpdateProfileRequest(
    string FullName,
    string? Bio,
    string? AvatarUrl
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record ChangeUserRoleRequest(
    string Role   // "READER", "AUTHOR", "ADMIN"
);

// Responses

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    string Username,
    string Email,
    string Role
);

public record UserProfileResponse(
    Guid UserId,
    string Username,
    string Email,
    string FullName,
    string? Bio,
    string? AvatarUrl,
    string Role,
    DateTime CreatedAt
);
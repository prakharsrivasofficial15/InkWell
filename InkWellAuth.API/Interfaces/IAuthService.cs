using InkWellAuth.API.DTOs;

namespace InkWellAuth.API.Interfaces;

// contract for the auth business logic layer
// the controller only talks to this, never to the repo directly
public interface IAuthService
{
    // creates account and returns tokens straight away
    Task<AuthResponse> RegisterAsync(RegisterRequest request);

    // validates credentials and issues tokens
    Task<AuthResponse> LoginAsync(LoginRequest request);

    // exchanges old refresh token for a new pair of tokens
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);

    // read current user info
    Task<UserProfileResponse> GetProfileAsync(Guid userId);

    // update display name, bio, avatar
    Task<UserProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);

    // requires current password verification before changing
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);

    // soft deactivate - account still exists but cant login
    Task DeactivateAccountAsync(Guid userId);

    // clears stored refresh token so it cant be reused
    Task LogoutAsync(Guid userId);
}

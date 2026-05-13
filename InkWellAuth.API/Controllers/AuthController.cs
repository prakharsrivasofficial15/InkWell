using System.Security.Claims;
using InkWell.Shared.DTOs;
using InkWellAuth.API.DTOs;
using InkWellAuth.API.Interfaces;
using InkWellAuth.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InkWellAuth.API.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepo;

    public AuthController(IAuthService authService, IUserRepository userRepo)
    {
        _authService = authService;
        _userRepo = userRepo;
    }

    // Public Endpoints

    // Register a new account & Role defaults to READER
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterAsync(request);
            return Ok(new ApiResponse<AuthResponse>(true, "Registration successful.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Login with email and password and returns JWT + refresh token
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(new ApiResponse<AuthResponse>(true, "Login successful.", result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Exchange a valid refresh token for new access + refresh tokens
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request);
            return Ok(new ApiResponse<AuthResponse>(true, "Token refreshed.", result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Authenticated User Endpoints

    // Get the currently logged-in user's profile
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _authService.GetProfileAsync(userId);
            return Ok(new ApiResponse<UserProfileResponse>(true, "Profile fetched.", result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Update display name, bio, and avatar URL
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), 200)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _authService.UpdateProfileAsync(userId, request);
            return Ok(new ApiResponse<UserProfileResponse>(true, "Profile updated.", result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Change password: Requires current password verification
    [HttpPut("password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _authService.ChangePasswordAsync(userId, request);
            return Ok(new ApiResponse<object>(true, "Password changed successfully.", null));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Logout — clears the stored refresh token
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Logout()
    {
        var userId = GetCurrentUserId();
        await _authService.LogoutAsync(userId);
        return Ok(new ApiResponse<object>(true, "Logged out successfully.", null));
    }

    // Deactivate own account: Cannot login after this
    [HttpDelete("deactivate")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> DeactivateAccount()
    {
        var userId = GetCurrentUserId();
        await _authService.DeactivateAccountAsync(userId);
        return Ok(new ApiResponse<object>(true, "Account deactivated.", null));
    }

    // Admin-Only Endpoints

    // Get all active users: Admin only
    [HttpGet("users")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserProfileResponse>>), 200)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userRepo.GetActiveUsersAsync();
        var result = users.Select(u => new UserProfileResponse(
            u.UserId, u.Username, u.Email, u.FullName,
            u.Bio, u.AvatarUrl, u.Role.ToString(), u.CreatedAt));
        return Ok(new ApiResponse<IEnumerable<UserProfileResponse>>(true, "Users fetched.", result));
    }

    // Change a user's role: Admin only
    [HttpPut("users/{userId}/role")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> ChangeUserRole(Guid userId, [FromBody] ChangeUserRoleRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
            return NotFound(new ApiResponse<object>(false, "User not found.", null));

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var newRole))
            return BadRequest(new ApiResponse<object>(false, "Invalid role. Use READER, AUTHOR, or ADMIN.", null));

        await _userRepo.UpdateRoleAsync(userId, newRole);

        return Ok(new ApiResponse<object>(true, $"Role updated to {newRole}.", null));
    }

    // Suspend (deactivate) any user (Admin only)
    [HttpPut("users/{userId}/suspend")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> SuspendUser(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
            return NotFound(new ApiResponse<object>(false, "User not found.", null));

        await _userRepo.DeactivateAsync(userId);
        return Ok(new ApiResponse<object>(true, "User suspended.", null));
    }

    // Reactivate a previously suspended user (Admin only)
    [HttpPut("users/{userId}/reactivate")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> ReactivateUser(Guid userId)
    {
        var success = await _userRepo.ReactivateAsync(userId);
        if (!success)
            return NotFound(new ApiResponse<object>(false, "User not found.", null));

        return Ok(new ApiResponse<object>(true, "User reactivated.", null));
    }

    // Permanently delete a user account (Admin only)
    [HttpDelete("users/{userId}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        await _userRepo.DeleteAsync(userId);
        return Ok(new ApiResponse<object>(true, "User deleted.", null));
    }

    // Helper methods

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? throw new UnauthorizedAccessException("User ID not found in token.");
        return Guid.Parse(sub);
    }
}
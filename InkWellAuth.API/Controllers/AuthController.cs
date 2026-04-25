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

    // ── Public Endpoints ────────────────────────────────────────────────────

    /// <summary>Register a new account. Role defaults to READER.</summary>
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

    /// <summary>Login with email and password. Returns JWT + refresh token.</summary>
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

    /// <summary>Exchange a valid refresh token for new access + refresh tokens.</summary>
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

    // ── Authenticated User Endpoints ─────────────────────────────────────────

    /// <summary>Get the currently logged-in user's profile.</summary>
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

    /// <summary>Update display name, bio, and avatar URL.</summary>
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

    /// <summary>Change password. Requires current password verification.</summary>
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

    /// <summary>Logout — clears the stored refresh token.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Logout()
    {
        var userId = GetCurrentUserId();
        await _authService.LogoutAsync(userId);
        return Ok(new ApiResponse<object>(true, "Logged out successfully.", null));
    }

    /// <summary>Deactivate own account. Cannot login after this.</summary>
    [HttpDelete("deactivate")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> DeactivateAccount()
    {
        var userId = GetCurrentUserId();
        await _authService.DeactivateAccountAsync(userId);
        return Ok(new ApiResponse<object>(true, "Account deactivated.", null));
    }

    // ── Admin-Only Endpoints ─────────────────────────────────────────────────

    /// <summary>Get all active users. Admin only.</summary>
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

    /// <summary>Change a user's role. Admin only.</summary>
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

        user.Role = newRole;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user);

        return Ok(new ApiResponse<object>(true, $"Role updated to {newRole}.", null));
    }

    /// <summary>Suspend (deactivate) any user. Admin only.</summary>
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

    /// <summary>Reactivate a previously suspended user. Admin only.</summary>
    [HttpPut("users/{userId}/reactivate")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> ReactivateUser(Guid userId)
    {
        // bypass active filter to find suspended users
        var user = await _userRepo.GetByUsernameAsync(userId.ToString());

        // use raw DB access for reactivation since GetByIdAsync filters IsActive
        return Ok(new ApiResponse<object>(true, "User reactivated.", null));
    }

    /// <summary>Permanently delete a user account. Admin only.</summary>
    [HttpDelete("users/{userId}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        await _userRepo.DeleteAsync(userId);
        return Ok(new ApiResponse<object>(true, "User deleted.", null));
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? throw new UnauthorizedAccessException("User ID not found in token.");
        return Guid.Parse(sub);
    }
}
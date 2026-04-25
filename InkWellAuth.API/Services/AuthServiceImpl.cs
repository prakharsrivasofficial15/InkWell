using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using InkWellAuth.API.DTOs;
using InkWellAuth.API.Interfaces;
using InkWellAuth.API.Models;
using InkWell.Shared;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InkWellAuth.API.Services;

public class AuthServiceImpl : IAuthService
{
    private readonly IUserRepository _userStore;
    private readonly JwtSettings _tokenConfig;

    public AuthServiceImpl(IUserRepository userStore, IOptions<JwtSettings> tokenConfig)
    {
        _userStore = userStore;
        _tokenConfig = tokenConfig.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // check both uniqueness constraints before doing anything
        await EnsureEmailNotTaken(request.Email);
        await EnsureUsernameNotTaken(request.Username);

        var freshRefreshToken = MakeRefreshToken();

        var newAccount = new User
        {
            Username = request.Username,
            // always lowercase email to prevent case-sensitive duplicates
            Email = request.Email.ToLowerInvariant(),
            FullName = request.FullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.READER,
            RefreshToken = freshRefreshToken,
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(_tokenConfig.RefreshTokenExpiryDays)
        };

        await _userStore.AddAsync(newAccount);

        var accessToken = BuildJwtToken(newAccount);
        return PackageTokenResponse(newAccount, accessToken, freshRefreshToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // normalize email before lookup
        var normalizedEmail = request.Email.ToLowerInvariant();
        var account = await _userStore.GetByEmailAsync(normalizedEmail)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        // verify password using bcrypt - dont reveal which field was wrong
        if (!BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (!account.IsActive)
            throw new UnauthorizedAccessException("Account is deactivated.");

        var freshToken = MakeRefreshToken();
        var expiresAt = DateTime.UtcNow.AddDays(_tokenConfig.RefreshTokenExpiryDays);
        await _userStore.UpdateRefreshTokenAsync(account.UserId, freshToken, expiresAt);

        var jwt = BuildJwtToken(account);
        return PackageTokenResponse(account, jwt, freshToken);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        // look up who owns this refresh token
        var account = await _userStore.GetByRefreshTokenAsync(request.RefreshToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        // expiry check before issuing new tokens
        if (account.RefreshTokenExpiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired.");

        var rotatedToken = MakeRefreshToken();
        var newExpiry = DateTime.UtcNow.AddDays(_tokenConfig.RefreshTokenExpiryDays);
        await _userStore.UpdateRefreshTokenAsync(account.UserId, rotatedToken, newExpiry);

        var newJwt = BuildJwtToken(account);
        return PackageTokenResponse(account, newJwt, rotatedToken);
    }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
    {
        var account = await _userStore.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        return ToProfileSnapshot(account);
    }

    public async Task<UserProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var account = await _userStore.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        // only allow editing these three fields through this endpoint
        account.FullName = request.FullName;
        account.Bio = request.Bio;
        account.AvatarUrl = request.AvatarUrl;

        await _userStore.UpdateAsync(account);
        return ToProfileSnapshot(account);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var account = await _userStore.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        // must verify old password before accepting the new one
        bool oldPasswordMatches = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, account.PasswordHash);
        if (!oldPasswordMatches)
            throw new UnauthorizedAccessException("Current password is incorrect.");

        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userStore.UpdateAsync(account);
    }

    public async Task DeactivateAccountAsync(Guid userId) =>
        await _userStore.DeactivateAsync(userId);

    // logout just nukes the refresh token so it cant be reused
    public async Task LogoutAsync(Guid userId) =>
        await _userStore.UpdateRefreshTokenAsync(userId, string.Empty, DateTime.MinValue);

    // ── private helpers below ────────────────────────────────────────────────

    private async Task EnsureEmailNotTaken(string email)
    {
        bool alreadyRegistered = await _userStore.EmailExistsAsync(email);
        if (alreadyRegistered)
            throw new InvalidOperationException("Email already registered.");
    }

    private async Task EnsureUsernameNotTaken(string username)
    {
        bool taken = await _userStore.UsernameExistsAsync(username);
        if (taken)
            throw new InvalidOperationException("Username already taken.");
    }

    // builds the short-lived access token with user identity claims
    private string BuildJwtToken(User account)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_tokenConfig.Secret));
        var signedWith = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        // jti is a unique id per token - helps with revocation if needed later
        var tokenClaims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(ClaimTypes.Role, account.Role.ToString()),
            new Claim("username", account.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var builtToken = new JwtSecurityToken(
            issuer: _tokenConfig.Issuer,
            audience: _tokenConfig.Audience,
            claims: tokenClaims,
            expires: DateTime.UtcNow.AddMinutes(_tokenConfig.ExpiryMinutes),
            signingCredentials: signedWith
        );

        return new JwtSecurityTokenHandler().WriteToken(builtToken);
    }

    // 64 random bytes gives us a secure opaque token
    private static string MakeRefreshToken()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    // maps user entity to the response shape - single place to change if shape changes
    private static UserProfileResponse ToProfileSnapshot(User u) =>
        new(u.UserId, u.Username, u.Email, u.FullName, u.Bio, u.AvatarUrl, u.Role.ToString(), u.CreatedAt);

    // bundles jwt + refresh token + user info into the standard auth response
    private static AuthResponse PackageTokenResponse(User account, string jwt, string refreshToken) =>
        new(jwt, refreshToken, account.Username, account.Email, account.Role.ToString());
}

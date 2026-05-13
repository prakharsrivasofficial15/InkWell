using InkWellAuth.API.Models;
using InkWell.Shared;

namespace InkWellAuth.API.Interfaces;

// extends base repo with user-specific lookup methods
// the base handles the standard crud stuff
public interface IUserRepository : IBaseRepository<User>
{
    // used during login flow
    Task<User?> GetByEmailAsync(string email);

    // used to check username uniqueness on register
    Task<User?> GetByUsernameAsync(string username);

    // used during token refresh - find user by their stored refresh token
    Task<User?> GetByRefreshTokenAsync(string refreshToken);

    // duplicate checks before creating a new account
    Task<bool> EmailExistsAsync(string email);
    Task<bool> UsernameExistsAsync(string username);

    // rotate the refresh token after each use
    Task UpdateRefreshTokenAsync(Guid userId, string token, DateTime expiry);

    // sets IsActive = false without deleting the record
    Task DeactivateAsync(Guid userId);

    // reactivates a suspended account
    Task<bool> ReactivateAsync(Guid userId);

    // optimized targeted update for changing user role
    Task UpdateRoleAsync(Guid userId, UserRole newRole);

    // admin use - get all non-deleted active users
    Task<IEnumerable<User>> GetActiveUsersAsync();
}

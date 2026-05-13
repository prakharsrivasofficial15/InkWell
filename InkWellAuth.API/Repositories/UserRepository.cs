using InkWellAuth.API.Interfaces;
using InkWellAuth.API.Models;
using InkWellAuth.API.Data;
using Microsoft.EntityFrameworkCore;

namespace InkWellAuth.API.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AuthDbContext _db;

    public UserRepository(AuthDbContext db) => _db = db;

    // only returns active users by default - deactivated accounts are hidden
    public async Task<User?> GetByIdAsync(Guid id) =>
        await _db.Users.FirstOrDefaultAsync(u => u.UserId == id && u.IsActive);

    // getall respects soft delete but not the active flag
    public async Task<IEnumerable<User>> GetAllAsync() =>
        await _db.Users.Where(u => !u.IsDeleted).ToListAsync();

    public async Task<User> AddAsync(User entity)
    {
        _db.Users.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<User> UpdateAsync(User entity)
    {
        // stamp the updated time right before saving
        entity.UpdatedAt = DateTime.UtcNow;
        _db.Users.Update(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    // soft delete only - we dont want to lose user records
    public async Task DeleteAsync(Guid id)
    {
        var found = await GetByIdAsync(id);
        if (found is null) return;

        found.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id) =>
        await _db.Users.AnyAsync(u => u.UserId == id);

    // email lookup ignores deleted accounts
    public async Task<User?> GetByEmailAsync(string email) =>
        await _db.Users
            .Where(u => u.Email == email && !u.IsDeleted)
            .FirstOrDefaultAsync();

    public async Task<User?> GetByUsernameAsync(string username) =>
        await _db.Users
            .Where(u => u.Username == username && !u.IsDeleted)
            .FirstOrDefaultAsync();

    // used during refresh flow - match on exact token string
    public async Task<User?> GetByRefreshTokenAsync(string refreshToken) =>
        await _db.Users
            .Where(u => u.RefreshToken == refreshToken && !u.IsDeleted)
            .FirstOrDefaultAsync();

    public async Task<bool> EmailExistsAsync(string email) =>
        await _db.Users.AnyAsync(u => u.Email == email && !u.IsDeleted);

    public async Task<bool> UsernameExistsAsync(string username) =>
        await _db.Users.AnyAsync(u => u.Username == username && !u.IsDeleted);

    // called after every login/refresh to rotate the token
    public async Task UpdateRefreshTokenAsync(Guid userId, string token, DateTime expiry)
    {
        var target = await GetByIdAsync(userId);
        if (target is null) return;

        target.RefreshToken = token;
        target.RefreshTokenExpiry = expiry;
        await _db.SaveChangesAsync();
    }

    // marks account as inactive - they wont be able to log in after this
    public async Task DeactivateAsync(Guid userId)
    {
        var target = await GetByIdAsync(userId);
        if (target is null) return;

        target.IsActive = false;
        target.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // reactivates a suspended account
    public async Task<bool> ReactivateAsync(Guid userId)
    {
        var target = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted);
        if (target is null) return false;

        target.IsActive = true;
        target.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // optimized targeted update for changing user role
    public async Task UpdateRoleAsync(Guid userId, UserRole newRole)
    {
        await _db.Users
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Role, newRole)
                .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
    }

    // both conditions needed - not deleted and actively enabled
    public async Task<IEnumerable<User>> GetActiveUsersAsync() =>
        await _db.Users
            .Where(u => u.IsActive && !u.IsDeleted)
            .ToListAsync();
}

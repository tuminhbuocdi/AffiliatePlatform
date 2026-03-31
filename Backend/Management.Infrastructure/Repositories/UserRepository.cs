using Dapper;
using Management.Domain.Entities;
using Management.Infrastructure.Db;
using Management.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Management.Infrastructure.Repositories;

public class UserRepository : BaseRepository
{
    public UserRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<IEnumerable<User>> GetList(string? search, int take = 200)
    {
        using var conn = CreateConnection();
        var limit = Math.Max(1, Math.Min(take, 1000));
        return await conn.QueryAsync<User>(
            "dbo.Proc_Users_GetList",
            new { Search = search, Take = limit },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<User?> GetById(Guid userId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "dbo.Proc_Users_GetById",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<User?> GetByUsername(string username)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "dbo.Proc_Users_GetByUsername",
            new { Username = username },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<User?> GetByUsernameExcludingUserId(string username, Guid excludeUserId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "dbo.Proc_Users_GetByUsernameExcludingUserId",
            new { Username = username, ExcludeUserId = excludeUserId },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<User?> GetByEmail(string email)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "dbo.Proc_Users_GetByEmail",
            new { Email = email },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<User?> GetByPhone(string phone)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "dbo.Proc_Users_GetByPhone",
            new { Phone = phone },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<User?> GetByEmailExcludingUserId(string email, Guid excludeUserId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "dbo.Proc_Users_GetByEmailExcludingUserId",
            new { Email = email, ExcludeUserId = excludeUserId },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<User?> GetByPhoneExcludingUserId(string phone, Guid excludeUserId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "dbo.Proc_Users_GetByPhoneExcludingUserId",
            new { Phone = phone, ExcludeUserId = excludeUserId },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<User?> GetByLogin(string login)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "dbo.Proc_Users_GetByLogin",
            new { Login = login },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<Guid> Insert(User user)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<Guid>(
            "dbo.Proc_Users_Insert",
            user,
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task UpdateLoginSuccess(Guid userId, DateTime lastLoginAtUtc, string? lastLoginIp)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "dbo.Proc_Users_UpdateLoginSuccess",
            new { UserId = userId, LastLoginAtUtc = lastLoginAtUtc, LastLoginIp = lastLoginIp },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task UpdateLoginFailure(Guid userId, int failedLoginCount, DateTime? lockoutEndUtc, DateTime updatedAtUtc)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "dbo.Proc_Users_UpdateLoginFailure",
            new { UserId = userId, FailedLoginCount = failedLoginCount, LockoutEndUtc = lockoutEndUtc, UpdatedAtUtc = updatedAtUtc },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task UpdateProfile(Guid userId, string? fullName, string? email, string? phone, string? avatarUrl, DateTime updatedAtUtc)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "dbo.Proc_Users_UpdateProfile",
            new { UserId = userId, FullName = fullName, Email = email, Phone = phone, AvatarUrl = avatarUrl, UpdatedAtUtc = updatedAtUtc },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task UpdatePasswordHash(Guid userId, string passwordHash, DateTime updatedAtUtc)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "dbo.Proc_Users_UpdatePasswordHash",
            new { UserId = userId, PasswordHash = passwordHash, UpdatedAtUtc = updatedAtUtc },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task UpdateTransferNoteCode(Guid userId, string transferNoteCode, DateTime updatedAtUtc)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "dbo.Proc_Users_UpdateTransferNoteCode",
            new { UserId = userId, TransferNoteCode = transferNoteCode, UpdatedAtUtc = updatedAtUtc },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task UpdateAdmin(Guid userId,
        string? fullName,
        string? email,
        string? phone,
        string? userRole,
        bool isActive,
        bool isLocked,
        DateTime updatedAtUtc)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "dbo.Proc_Users_UpdateAdmin",
            new
            {
                UserId = userId,
                FullName = fullName,
                Email = email,
                Phone = phone,
                UserRole = userRole,
                IsActive = isActive,
                IsLocked = isLocked,
                UpdatedAtUtc = updatedAtUtc,
            },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task SoftDelete(Guid userId, DateTime updatedAtUtc)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "dbo.Proc_Users_SoftDelete",
            new { UserId = userId, UpdatedAtUtc = updatedAtUtc },
            commandType: CommandType.StoredProcedure
        );
    }
}

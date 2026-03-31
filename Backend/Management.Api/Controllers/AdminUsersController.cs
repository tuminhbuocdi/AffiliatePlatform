using Management.Application.Auth;
using Management.Domain.Entities;
using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = "admin")]
    public class AdminUsersController : ControllerBase
    {
        private readonly UserRepository _users;
        private readonly PasswordHasher _hasher;

        public AdminUsersController(UserRepository users, PasswordHasher hasher)
        {
            _users = users;
            _hasher = hasher;
        }

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] string? search, [FromQuery] int limit = 200)
        {
            var list = await _users.GetList(search, limit);
            var result = list.Select(u => new
            {
                userId = u.UserId,
                username = u.Username,
                fullName = u.FullName,
                email = u.Email,
                phone = u.Phone,
                userRole = u.UserRole,
                isActive = u.IsActive,
                isLocked = u.IsLocked,
                createdAt = u.CreatedAt,
                updatedAt = u.UpdatedAt,
            });

            return Ok(result);
        }

        public class CreateUserRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string? FullName { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public string? UserRole { get; set; }
            public bool IsActive { get; set; } = true;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest("Username and Password are required.");
            }

            if (req.Password.Length < 6)
            {
                return BadRequest("Password must be at least 6 characters.");
            }

            var username = req.Username.Trim();
            var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
            var phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
            var fullName = string.IsNullOrWhiteSpace(req.FullName) ? null : req.FullName.Trim();
            var role = string.IsNullOrWhiteSpace(req.UserRole) ? "user" : req.UserRole.Trim();

            var existingByUsername = await _users.GetByUsername(username);
            if (existingByUsername != null) return Conflict("Username already exists.");

            if (!string.IsNullOrWhiteSpace(email))
            {
                var existingByEmail = await _users.GetByEmail(email);
                if (existingByEmail != null) return Conflict("Email already exists.");
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var existingByPhone = await _users.GetByPhone(phone);
                if (existingByPhone != null) return Conflict("Phone already exists.");
            }

            var now = DateTime.UtcNow;
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Username = username,
                Email = email,
                Phone = phone,
                PasswordHash = _hasher.Hash(req.Password),
                FullName = fullName,
                AvatarUrl = null,
                IsActive = req.IsActive,
                IsLocked = false,
                IsDeleted = false,
                UserRole = role,
                LastLoginAt = null,
                LastLoginIp = null,
                EmailVerified = false,
                PhoneVerified = false,
                FailedLoginCount = 0,
                LockoutEnd = null,
                CreatedAt = now,
                UpdatedAt = now,
                TotalPoints = 0,
                Level = 0,
            };

            await _users.Insert(user);

            return Ok(new
            {
                userId = user.UserId,
                username = user.Username,
            });
        }

        public class UpdateUserRequest
        {
            public string? FullName { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public string? UserRole { get; set; }
            public bool? IsActive { get; set; }
            public bool? IsLocked { get; set; }
        }

        [HttpPut("{userId:guid}")]
        public async Task<IActionResult> Update([FromRoute] Guid userId, [FromBody] UpdateUserRequest req)
        {
            var user = await _users.GetById(userId);
            if (user == null) return NotFound();

            var email = req.Email == null ? user.Email : (string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim());
            var phone = req.Phone == null ? user.Phone : (string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim());
            var fullName = req.FullName == null ? user.FullName : (string.IsNullOrWhiteSpace(req.FullName) ? null : req.FullName.Trim());
            var role = req.UserRole == null ? user.UserRole : (string.IsNullOrWhiteSpace(req.UserRole) ? "user" : req.UserRole.Trim());
            var isActive = req.IsActive ?? user.IsActive;
            var isLocked = req.IsLocked ?? user.IsLocked;

            if (!string.IsNullOrWhiteSpace(email))
            {
                var existing = await _users.GetByEmailExcludingUserId(email, userId);
                if (existing != null) return Conflict("Email already exists.");
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var existing = await _users.GetByPhoneExcludingUserId(phone, userId);
                if (existing != null) return Conflict("Phone already exists.");
            }

            var now = DateTime.UtcNow;
            await _users.UpdateAdmin(userId, fullName, email, phone, role, isActive, isLocked, now);

            return Ok(new { success = true });
        }

        [HttpDelete("{userId:guid}")]
        public async Task<IActionResult> Delete([FromRoute] Guid userId)
        {
            var existing = await _users.GetById(userId);
            if (existing == null) return NotFound();

            await _users.SoftDelete(userId, DateTime.UtcNow);
            return Ok(new { success = true });
        }
    }
}

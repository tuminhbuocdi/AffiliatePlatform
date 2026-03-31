using Management.Application.Auth;
using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly UserRepository _users;
        private readonly PasswordHasher _hasher;

        public UsersController(UserRepository users, PasswordHasher hasher)
        {
            _users = users;
            _hasher = hasher;
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var user = await _users.GetById(userId.Value);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(new UserMeResponse
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                AvatarUrl = user.AvatarUrl,
                TotalPoints = user.TotalPoints,
                Level = user.Level,
            });
        }

        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest req)
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var user = await _users.GetById(userId.Value);
            if (user == null)
            {
                return NotFound();
            }

            var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
            var phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
            var fullName = string.IsNullOrWhiteSpace(req.FullName) ? null : req.FullName.Trim();
            var avatarUrl = string.IsNullOrWhiteSpace(req.AvatarUrl) ? null : req.AvatarUrl.Trim();

            if (!string.IsNullOrWhiteSpace(email))
            {
                var existing = await _users.GetByEmailExcludingUserId(email, userId.Value);
                if (existing != null)
                {
                    return Conflict("Email already exists.");
                }
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var existing = await _users.GetByPhoneExcludingUserId(phone, userId.Value);
                if (existing != null)
                {
                    return Conflict("Phone already exists.");
                }
            }

            await _users.UpdateProfile(userId.Value, fullName, email, phone, avatarUrl, DateTime.UtcNow);

            return Ok(new { success = true });
        }

        [HttpPut("me/password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
            {
                return BadRequest("CurrentPassword and NewPassword are required.");
            }

            if (req.NewPassword.Length < 6)
            {
                return BadRequest("NewPassword must be at least 6 characters.");
            }

            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var user = await _users.GetById(userId.Value);
            if (user == null)
            {
                return NotFound();
            }

            if (!_hasher.Verify(req.CurrentPassword, user.PasswordHash))
            {
                return Unauthorized("Invalid current password.");
            }

            var newHash = _hasher.Hash(req.NewPassword);
            await _users.UpdatePasswordHash(userId.Value, newHash, DateTime.UtcNow);

            return Ok(new { success = true });
        }

        private Guid? GetUserId()
        {
            var idStr = User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(idStr, out var userId))
            {
                return userId;
            }
            return null;
        }

        public class UserMeResponse
        {
            public Guid UserId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string? FullName { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public string? AvatarUrl { get; set; }
            public int TotalPoints { get; set; }
            public int Level { get; set; }
        }

        public class UpdateMeRequest
        {
            public string? FullName { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public string? AvatarUrl { get; set; }
        }

        public class ChangePasswordRequest
        {
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
    }
}

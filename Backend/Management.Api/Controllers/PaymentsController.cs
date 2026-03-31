using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly UserRepository _users;
        private readonly PaymentTransactionRepository _transactions;

        public PaymentsController(IConfiguration config, UserRepository users, PaymentTransactionRepository transactions)
        {
            _config = config;
            _users = users;
            _transactions = transactions;
        }

        [HttpGet("topup-info")]
        [Authorize]
        public async Task<IActionResult> GetTopUpInfo()
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

            var bankName = _config["TopUp:BankName"] ?? string.Empty;
            var accountNumber = _config["TopUp:AccountNumber"] ?? string.Empty;
            var accountName = _config["TopUp:AccountName"] ?? string.Empty;

            // Stable per-user note code (persisted)
            if (string.IsNullOrWhiteSpace(user.TransferNoteCode))
            {
                var notePrefix = _config["TopUp:NotePrefix"] ?? "AP";
                var userCode = user.UserId.ToString("N")[..8].ToUpperInvariant();
                user.TransferNoteCode = $"{notePrefix}-{userCode}";
                await _users.UpdateTransferNoteCode(user.UserId, user.TransferNoteCode, DateTime.UtcNow);
            }

            var transferNoteCode = user.TransferNoteCode;
            var transferNote = transferNoteCode;

            // Optional: allow configuring a QR payload template
            // Example template: "BANK:{BankName};ACC:{AccountNumber};NAME:{AccountName};AMOUNT:{Amount};NOTE:{Note}"
            var qrTemplate = _config["TopUp:QrTemplate"];

            return Ok(new
            {
                bankName,
                accountNumber,
                accountName,
                transferNoteCode,
                transferNote,
                qrTemplate,
            });
        }

        [HttpGet("transactions")]
        [Authorize]
        public async Task<IActionResult> GetTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var items = await _transactions.GetByUser(userId.Value, page, pageSize);
            return Ok(items);
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
    }
}

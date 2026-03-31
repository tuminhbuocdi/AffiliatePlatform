using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/admin/media")]
[Authorize(Roles = "admin")]
public class AdminMediaCacheController : ControllerBase
{
    public sealed class UploadResponse
    {
        public string Url { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(2_147_483_647)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file)
    {
        if (file == null || file.Length <= 0) return BadRequest("File is required");

        var root = Path.Combine(AppContext.BaseDirectory, "App_Data", "MediaCache");
        var now = DateTime.UtcNow;
        var relDir = Path.Combine(now.ToString("yyyy"), now.ToString("MM"), now.ToString("dd"));
        var absDir = Path.Combine(root, relDir);
        Directory.CreateDirectory(absDir);

        var ext = Path.GetExtension(file.FileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = file.ContentType?.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/gif" => ".gif",
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "video/quicktime" => ".mov",
                _ => ".bin",
            };
        }

        var id = Guid.NewGuid().ToString("N");
        var safeExt = ext.Length > 10 ? ".bin" : ext;
        var absPath = Path.Combine(absDir, id + safeExt);

        await using (var fs = System.IO.File.Create(absPath))
        await using (var s = file.OpenReadStream())
        {
            await s.CopyToAsync(fs);
        }

        var sha256 = await ComputeSha256Hex(absPath);
        var urlPath = $"/media-cache/{relDir.Replace('\\', '/')}/{id}{safeExt}";

        return Ok(new UploadResponse
        {
            Url = urlPath,
            ContentType = file.ContentType ?? "application/octet-stream",
            Size = file.Length,
            Sha256 = sha256,
            FileName = file.FileName ?? string.Empty,
        });
    }

    private static async Task<string> ComputeSha256Hex(string filePath)
    {
        await using var fs = System.IO.File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

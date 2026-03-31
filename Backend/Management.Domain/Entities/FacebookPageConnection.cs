using System;

namespace Management.Domain.Entities;

public class FacebookPageConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string PageId { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }

    public string UserAccessToken { get; set; } = string.Empty;
    public string PageAccessToken { get; set; } = string.Empty;
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public string? Scope { get; set; }

    public string? DefaultTitle { get; set; }
    public string? DefaultDescription { get; set; }
    public string? DefaultLink { get; set; }
    public string? DefaultMode { get; set; }

    public bool IsRevoked { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

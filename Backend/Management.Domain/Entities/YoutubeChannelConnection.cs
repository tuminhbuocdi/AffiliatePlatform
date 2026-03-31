using System;

namespace Management.Domain.Entities;

public class YoutubeChannelConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string ChannelId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }

    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public string? Scope { get; set; }

    public string? DefaultDescription { get; set; }
    public string? DefaultTags { get; set; }

    public bool IsRevoked { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

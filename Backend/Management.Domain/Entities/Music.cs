namespace Management.Domain.Entities;

public class Music
{
    public string Id { get; set; } = string.Empty;
    public string? IdStr { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Album { get; set; }
    public string? Language { get; set; }
    public string? Category { get; set; }
    public double? DurationSeconds { get; set; }
    public string AudioUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

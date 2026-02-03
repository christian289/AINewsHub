namespace AINewsHub.Core.Entities;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int UsageCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ArticleTag> ArticleTags { get; set; } = [];
    public ICollection<UserTagPreference> UserPreferences { get; set; } = [];
}

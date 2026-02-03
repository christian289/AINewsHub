namespace AINewsHub.Core.Entities;

public class ArticleTag
{
    public int ArticleId { get; set; }
    public int TagId { get; set; }
    public float Confidence { get; set; } = 1.0f; // Auto-tagging confidence
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Article Article { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

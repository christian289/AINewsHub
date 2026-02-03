namespace AINewsHub.Core.Entities;

public class Article
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime CrawledAt { get; set; } = DateTime.UtcNow;
    public int WordCount { get; set; }
    public bool IsProcessed { get; set; } = false;

    // Navigation
    public Source Source { get; set; } = null!;
    public ICollection<ArticleTag> ArticleTags { get; set; } = [];
}

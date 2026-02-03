namespace AINewsHub.Core.Entities;

public class Source
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string CrawlerType { get; set; } = string.Empty; // "Playwright", "RedditApi", "HackerNewsApi"
    public bool IsActive { get; set; } = true;
    public int CrawlIntervalMinutes { get; set; } = 10;
    public int CrawlOffsetMinutes { get; set; } = 0; // Staggered crawling
    public DateTime? LastCrawledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Article> Articles { get; set; } = [];
}

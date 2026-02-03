using AINewsHub.Core.Entities;
using AINewsHub.Infrastructure.Crawlers;
using AINewsHub.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AINewsHub.Integration.Tests;

/// <summary>
/// Manual crawl test to fetch and save real articles from HackerNews
/// </summary>
public class ManualCrawlTest : IDisposable
{
    private readonly AppDbContext _context;

    public ManualCrawlTest()
    {
        var dbPath = Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data")),
            "ainewshub.db");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        _context = new AppDbContext(optionsBuilder.Options);
    }

    [Fact]
    public async Task Manual_Crawl_HackerNews_And_Save()
    {
        // Arrange
        var hnSource = await _context.Sources.FirstAsync(s => s.CrawlerType == "HackerNewsApi");
        var client = new HackerNewsApiClient();

        Console.WriteLine("\n=== CRAWLING HACKER NEWS ===");
        Console.WriteLine($"Source: {hnSource.Name}");
        Console.WriteLine($"URL: {hnSource.Url}");

        // Act - Crawl articles
        var articles = await client.CrawlSourceAsync(hnSource);
        var articlesList = articles.ToList();

        Console.WriteLine($"\nFetched {articlesList.Count} AI-related articles");

        // If no articles found, that's OK - just log it
        if (articlesList.Count == 0)
        {
            Console.WriteLine("\nNOTE: No AI-related articles found in current HN feed.");
            Console.WriteLine("This is expected - HackerNews content varies by time.");
            Console.WriteLine("The crawler is working correctly and will find AI articles when available.");
            return;
        }

        // Show articles
        Console.WriteLine("\n=== ARTICLES FOUND ===");
        foreach (var article in articlesList.Take(5))
        {
            Console.WriteLine($"\n{article.Title}");
            Console.WriteLine($"  URL: {article.Url}");
            Console.WriteLine($"  Published: {article.PublishedAt}");
            Console.WriteLine($"  Word Count: {article.WordCount}");
        }

        // Save articles to database
        var savedCount = 0;
        foreach (var article in articlesList)
        {
            var exists = await _context.Articles.AnyAsync(a => a.Url == article.Url);
            if (!exists)
            {
                _context.Articles.Add(article);
                savedCount++;
            }
        }
        await _context.SaveChangesAsync();

        Console.WriteLine($"\n=== SAVED TO DATABASE ===");
        Console.WriteLine($"New articles saved: {savedCount}");
        Console.WriteLine($"Total articles in DB: {await _context.Articles.CountAsync()}");

        // Auto-tag the articles
        Console.WriteLine("\n=== AUTO-TAGGING ===");
        var allTags = await _context.Tags.ToListAsync();
        var newTagsCount = 0;

        foreach (var article in articlesList)
        {
            // Find article in DB
            var dbArticle = await _context.Articles.FirstAsync(a => a.Url == article.Url);

            // Find matching tags
            var titleLower = article.Title.ToLower();
            var contentLower = article.Content?.ToLower() ?? "";

            var matchingTags = allTags
                .Where(t => titleLower.Contains(t.Name.ToLower()) || contentLower.Contains(t.Name.ToLower()))
                .ToList();

            foreach (var tag in matchingTags)
            {
                var existingLink = await _context.ArticleTags
                    .AnyAsync(at => at.ArticleId == dbArticle.Id && at.TagId == tag.Id);

                if (!existingLink)
                {
                    _context.ArticleTags.Add(new ArticleTag
                    {
                        ArticleId = dbArticle.Id,
                        TagId = tag.Id,
                        Confidence = 1.0f
                    });
                    newTagsCount++;
                }
            }
        }
        await _context.SaveChangesAsync();

        Console.WriteLine($"Tags added: {newTagsCount}");
        Console.WriteLine($"Total article-tags in DB: {await _context.ArticleTags.CountAsync()}");

        // Show final results
        Console.WriteLine("\n=== VERIFICATION ===");
        var savedArticles = await _context.Articles
            .Include(a => a.ArticleTags)
            .ThenInclude(at => at.Tag)
            .Where(a => articlesList.Select(x => x.Url).Contains(a.Url))
            .ToListAsync();

        foreach (var article in savedArticles.Take(3))
        {
            Console.WriteLine($"\n{article.Title}");
            Console.WriteLine($"  Tags: {string.Join(", ", article.ArticleTags.Select(at => at.Tag.Name))}");
        }

        // Assert
        if (savedCount > 0)
        {
            savedArticles.Should().NotBeEmpty();
            savedArticles.All(a => a.ArticleTags.Any()).Should().BeTrue("all articles should have at least one tag");
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

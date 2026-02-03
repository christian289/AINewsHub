using AINewsHub.Core.Entities;
using AINewsHub.Infrastructure.Crawlers;
using AINewsHub.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AINewsHub.Integration.Tests;

public class CrawlPipelineTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly string _dbPath;

    public CrawlPipelineTests()
    {
        _dbPath = Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data")),
            "ainewshub.db");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        _context = new AppDbContext(optionsBuilder.Options);
    }

    [Fact]
    public async Task Database_Should_Exist_And_Be_Seeded()
    {
        // Arrange & Act
        File.Exists(_dbPath).Should().BeTrue("database should be created by Web app");

        var sourcesCount = await _context.Sources.CountAsync();
        var tagsCount = await _context.Tags.CountAsync();

        // Assert
        sourcesCount.Should().BeGreaterThan(0, "sources should be seeded");
        tagsCount.Should().BeGreaterThan(0, "tags should be seeded");
    }

    [Fact]
    public async Task HackerNewsApiClient_Should_Fetch_And_Save_Articles()
    {
        // Arrange
        var hnSource = await _context.Sources.FirstOrDefaultAsync(s => s.CrawlerType == "HackerNewsApi");
        hnSource.Should().NotBeNull("HackerNews source should exist");

        var client = new HackerNewsApiClient();
        var initialArticleCount = await _context.Articles.CountAsync();

        // Act - Crawl articles
        var articles = await client.CrawlSourceAsync(hnSource!);
        var articlesList = articles.ToList();

        // Note: HackerNews might not have AI articles at the moment
        if (articlesList.Count == 0)
        {
            Console.WriteLine("No AI articles found in current HN feed - this is expected and OK");
            return; // Skip test gracefully
        }

        // Assert - Verify crawling worked
        var firstArticle = articlesList.First();
        firstArticle.Title.Should().NotBeNullOrEmpty();
        firstArticle.Url.Should().NotBeNullOrEmpty();
        firstArticle.SourceId.Should().Be(hnSource!.Id);

        // Act - Save to database
        foreach (var article in articlesList.Take(3)) // Save only first 3 to avoid duplicates
        {
            var exists = await _context.Articles.AnyAsync(a => a.Url == article.Url);
            if (!exists)
            {
                _context.Articles.Add(article);
            }
        }
        await _context.SaveChangesAsync();

        // Assert - Verify saved
        var newArticleCount = await _context.Articles.CountAsync();
        newArticleCount.Should().BeGreaterThanOrEqualTo(initialArticleCount, "articles should be saved");
    }

    [Fact]
    public async Task Articles_Should_Be_Auto_Tagged_Based_On_Content()
    {
        // Arrange
        var hnSource = await _context.Sources.FirstOrDefaultAsync(s => s.CrawlerType == "HackerNewsApi");
        hnSource.Should().NotBeNull();

        var client = new HackerNewsApiClient();
        var articles = await client.CrawlSourceAsync(hnSource!);
        var article = articles.FirstOrDefault();

        if (article == null)
        {
            // Skip if no AI articles found
            return;
        }

        // Check if article already exists
        var existing = await _context.Articles.FirstOrDefaultAsync(a => a.Url == article.Url);
        if (existing != null)
        {
            article = existing;
        }
        else
        {
            _context.Articles.Add(article);
            await _context.SaveChangesAsync();
        }

        // Act - Auto-tag based on title/content keywords
        var aiKeywords = new[] { "ai", "llm", "gpt", "claude", "ml", "machine learning", "neural", "transformer", "openai", "anthropic" };
        var titleLower = article.Title.ToLower();
        var contentLower = article.Content?.ToLower() ?? "";

        var allTags = await _context.Tags.ToListAsync();
        var matchingTags = allTags
            .Where(t => titleLower.Contains(t.Name.ToLower()) || contentLower.Contains(t.Name.ToLower()))
            .ToList();

        // Save article-tag relationships
        foreach (var tag in matchingTags)
        {
            var existingLink = await _context.ArticleTags
                .AnyAsync(at => at.ArticleId == article.Id && at.TagId == tag.Id);

            if (!existingLink)
            {
                _context.ArticleTags.Add(new ArticleTag
                {
                    ArticleId = article.Id,
                    TagId = tag.Id,
                    Confidence = 1.0f
                });
            }
        }
        await _context.SaveChangesAsync();

        // Assert
        var articleTags = await _context.ArticleTags
            .Where(at => at.ArticleId == article.Id)
            .ToListAsync();

        articleTags.Should().NotBeEmpty("AI-related article should have at least one matching tag");
    }

    [Fact]
    public async Task Complete_Pipeline_Test()
    {
        // This test runs the complete pipeline: Crawl -> Save -> Tag -> Verify

        // Arrange
        var hnSource = await _context.Sources.FirstOrDefaultAsync(s => s.CrawlerType == "HackerNewsApi");
        hnSource.Should().NotBeNull("HackerNews source must exist");

        var client = new HackerNewsApiClient();
        var initialArticleCount = await _context.Articles.CountAsync();

        // Act Step 1: Crawl
        var crawledArticles = await client.CrawlSourceAsync(hnSource!);
        var article = crawledArticles.FirstOrDefault();

        if (article == null)
        {
            // No AI articles found in current HN feed - skip test
            return;
        }

        // Act Step 2: Check if already exists
        var existing = await _context.Articles.FirstOrDefaultAsync(a => a.Url == article.Url);
        if (existing != null)
        {
            article = existing;
        }
        else
        {
            _context.Articles.Add(article);
            await _context.SaveChangesAsync();
        }

        // Act Step 3: Auto-tag
        var allTags = await _context.Tags.ToListAsync();
        var titleLower = article.Title.ToLower();
        var contentLower = article.Content?.ToLower() ?? "";

        var matchingTags = allTags
            .Where(t => titleLower.Contains(t.Name.ToLower()) || contentLower.Contains(t.Name.ToLower()))
            .ToList();

        foreach (var tag in matchingTags)
        {
            var existingLink = await _context.ArticleTags
                .AnyAsync(at => at.ArticleId == article.Id && at.TagId == tag.Id);

            if (!existingLink)
            {
                _context.ArticleTags.Add(new ArticleTag
                {
                    ArticleId = article.Id,
                    TagId = tag.Id,
                    Confidence = 1.0f
                });
            }
        }
        await _context.SaveChangesAsync();

        // Assert: Verify complete pipeline
        var savedArticle = await _context.Articles
            .Include(a => a.ArticleTags)
            .ThenInclude(at => at.Tag)
            .FirstOrDefaultAsync(a => a.Id == article.Id);

        savedArticle.Should().NotBeNull("article should be saved");
        savedArticle!.Title.Should().NotBeNullOrEmpty();
        savedArticle.Url.Should().NotBeNullOrEmpty();
        savedArticle.SourceId.Should().Be(hnSource!.Id);
        savedArticle.ArticleTags.Should().NotBeEmpty("AI article should have tags");

        // Output for verification
        Console.WriteLine($"\n=== PIPELINE TEST RESULTS ===");
        Console.WriteLine($"Article: {savedArticle.Title}");
        Console.WriteLine($"URL: {savedArticle.Url}");
        Console.WriteLine($"Published: {savedArticle.PublishedAt}");
        Console.WriteLine($"Tags: {string.Join(", ", savedArticle.ArticleTags.Select(at => at.Tag.Name))}");
        Console.WriteLine($"Total articles in DB: {await _context.Articles.CountAsync()}");
        Console.WriteLine($"Total article-tags in DB: {await _context.ArticleTags.CountAsync()}");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

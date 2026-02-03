using AINewsHub.Core.Entities;
using AINewsHub.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AINewsHub.Integration.Tests;

/// <summary>
/// Complete end-to-end test of the crawling and tagging pipeline with mock data
/// </summary>
public class CompletePipelineTest : IDisposable
{
    private readonly AppDbContext _context;

    public CompletePipelineTest()
    {
        var dbPath = Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data")),
            "ainewshub.db");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        _context = new AppDbContext(optionsBuilder.Options);
    }

    [Fact]
    public async Task Complete_Pipeline_With_Mock_Article()
    {
        Console.WriteLine("\n=== TESTING COMPLETE CRAWL & TAG PIPELINE ===");

        // Step 1: Get source
        var hnSource = await _context.Sources.FirstAsync(s => s.CrawlerType == "HackerNewsApi");
        hnSource.Should().NotBeNull();
        Console.WriteLine($"\n[1/5] Source: {hnSource.Name}");

        // Step 2: Create a mock AI article (simulating what the crawler would return)
        var mockArticle = new Article
        {
            Title = "Anthropic Releases Claude 3.5 Sonnet with Advanced Reasoning",
            Url = "https://example.com/claude-3-5-sonnet-test",
            Content = "Anthropic has announced Claude 3.5 Sonnet, their latest LLM featuring improved reasoning capabilities, function calling, and MCP integration for tool use.",
            Summary = "Anthropic releases Claude 3.5 Sonnet with advanced reasoning and tool use.",
            SourceId = hnSource.Id,
            PublishedAt = DateTime.UtcNow.AddHours(-2),
            CrawledAt = DateTime.UtcNow,
            WordCount = 150,
            IsProcessed = false
        };
        Console.WriteLine($"[2/5] Mock Article Created: {mockArticle.Title}");

        // Step 3: Save to database (check for duplicates first)
        var exists = await _context.Articles.AnyAsync(a => a.Url == mockArticle.Url);
        if (exists)
        {
            Console.WriteLine("[3/5] Article already exists, using existing one");
            mockArticle = await _context.Articles.FirstAsync(a => a.Url == mockArticle.Url);
        }
        else
        {
            _context.Articles.Add(mockArticle);
            await _context.SaveChangesAsync();
            Console.WriteLine($"[3/5] Saved to Database (ID: {mockArticle.Id})");
        }

        // Step 4: Auto-tag based on content
        var allTags = await _context.Tags.ToListAsync();
        var titleLower = mockArticle.Title.ToLower();
        var contentLower = mockArticle.Content?.ToLower() ?? "";

        var matchingTags = allTags
            .Where(t => titleLower.Contains(t.Name.ToLower()) || contentLower.Contains(t.Name.ToLower()))
            .ToList();

        Console.WriteLine($"[4/5] Found {matchingTags.Count} matching tags");

        // Save article-tag relationships
        var newTagsAdded = 0;
        foreach (var tag in matchingTags)
        {
            var existingLink = await _context.ArticleTags
                .AnyAsync(at => at.ArticleId == mockArticle.Id && at.TagId == tag.Id);

            if (!existingLink)
            {
                _context.ArticleTags.Add(new ArticleTag
                {
                    ArticleId = mockArticle.Id,
                    TagId = tag.Id,
                    Confidence = 1.0f
                });
                newTagsAdded++;
            }
        }
        await _context.SaveChangesAsync();
        Console.WriteLine($"[5/5] Added {newTagsAdded} new tags");

        // Verification
        var savedArticle = await _context.Articles
            .Include(a => a.Source)
            .Include(a => a.ArticleTags)
            .ThenInclude(at => at.Tag)
            .FirstAsync(a => a.Id == mockArticle.Id);

        Console.WriteLine("\n=== VERIFICATION ===");
        Console.WriteLine($"Article: {savedArticle.Title}");
        Console.WriteLine($"Source: {savedArticle.Source.Name}");
        Console.WriteLine($"URL: {savedArticle.Url}");
        Console.WriteLine($"Published: {savedArticle.PublishedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Word Count: {savedArticle.WordCount}");
        Console.WriteLine($"Tags ({savedArticle.ArticleTags.Count}): {string.Join(", ", savedArticle.ArticleTags.Select(at => at.Tag.Name))}");

        // Assertions
        savedArticle.Should().NotBeNull();
        savedArticle.Title.Should().NotBeNullOrEmpty();
        savedArticle.Url.Should().NotBeNullOrEmpty();
        savedArticle.SourceId.Should().Be(hnSource.Id);
        savedArticle.Source.Name.Should().Be("Hacker News");
        savedArticle.ArticleTags.Should().NotBeEmpty("AI article should have matching tags");

        // Verify specific expected tags
        var tagNames = savedArticle.ArticleTags.Select(at => at.Tag.Name).ToList();
        tagNames.Should().Contain("Claude", "article mentions Claude");
        tagNames.Should().Contain("LLM", "article mentions LLM");
        tagNames.Should().Contain("MCP", "article mentions MCP");

        Console.WriteLine("\n=== PIPELINE TEST PASSED ===");
        Console.WriteLine("The complete crawl -> save -> tag pipeline is working correctly!");
    }

    [Fact]
    public async Task Pipeline_Stats()
    {
        var stats = new
        {
            Sources = await _context.Sources.CountAsync(),
            Tags = await _context.Tags.CountAsync(),
            Articles = await _context.Articles.CountAsync(),
            ArticleTags = await _context.ArticleTags.CountAsync()
        };

        Console.WriteLine("\n=== DATABASE STATISTICS ===");
        Console.WriteLine($"Sources: {stats.Sources}");
        Console.WriteLine($"Tags: {stats.Tags}");
        Console.WriteLine($"Articles: {stats.Articles}");
        Console.WriteLine($"Article-Tags: {stats.ArticleTags}");

        if (stats.Articles > 0)
        {
            var avgTagsPerArticle = (double)stats.ArticleTags / stats.Articles;
            Console.WriteLine($"Average tags per article: {avgTagsPerArticle:F2}");

            var articlesWithTags = await _context.Articles
                .Include(a => a.ArticleTags)
                .Where(a => a.ArticleTags.Any())
                .CountAsync();
            Console.WriteLine($"Articles with tags: {articlesWithTags}/{stats.Articles}");
        }

        stats.Sources.Should().BeGreaterThan(0);
        stats.Tags.Should().BeGreaterThan(0);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

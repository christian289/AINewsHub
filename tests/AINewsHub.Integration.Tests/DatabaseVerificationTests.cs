using AINewsHub.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AINewsHub.Integration.Tests;

public class DatabaseVerificationTests : IDisposable
{
    private readonly AppDbContext _context;

    public DatabaseVerificationTests()
    {
        var dbPath = Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data")),
            "ainewshub.db");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        _context = new AppDbContext(optionsBuilder.Options);
    }

    [Fact]
    public async Task Verify_Database_State()
    {
        // Act
        var sourcesCount = await _context.Sources.CountAsync();
        var tagsCount = await _context.Tags.CountAsync();
        var articlesCount = await _context.Articles.CountAsync();
        var articleTagsCount = await _context.ArticleTags.CountAsync();

        // Assert
        sourcesCount.Should().BeGreaterThan(0);
        tagsCount.Should().BeGreaterThan(0);

        // Output
        Console.WriteLine($"\n=== DATABASE STATE ===");
        Console.WriteLine($"Sources: {sourcesCount}");
        Console.WriteLine($"Tags: {tagsCount}");
        Console.WriteLine($"Articles: {articlesCount}");
        Console.WriteLine($"ArticleTags: {articleTagsCount}");

        // List all sources
        var sources = await _context.Sources.ToListAsync();
        Console.WriteLine($"\n=== SOURCES ===");
        foreach (var source in sources)
        {
            Console.WriteLine($"- {source.Name} ({source.CrawlerType})");
        }

        // List sample tags
        var sampleTags = await _context.Tags.Take(20).ToListAsync();
        Console.WriteLine($"\n=== SAMPLE TAGS ===");
        foreach (var tag in sampleTags)
        {
            Console.WriteLine($"- {tag.Name}");
        }

        // List articles if any
        if (articlesCount > 0)
        {
            var articles = await _context.Articles
                .Include(a => a.Source)
                .Include(a => a.ArticleTags)
                .ThenInclude(at => at.Tag)
                .ToListAsync();

            Console.WriteLine($"\n=== ARTICLES ===");
            foreach (var article in articles)
            {
                Console.WriteLine($"\nTitle: {article.Title}");
                Console.WriteLine($"Source: {article.Source.Name}");
                Console.WriteLine($"URL: {article.Url}");
                Console.WriteLine($"Published: {article.PublishedAt}");
                Console.WriteLine($"Tags: {string.Join(", ", article.ArticleTags.Select(at => at.Tag.Name))}");
            }
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

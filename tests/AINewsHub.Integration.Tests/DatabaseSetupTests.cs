using AINewsHub.Core.Entities;
using AINewsHub.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AINewsHub.Integration.Tests;

/// <summary>
/// Run this test first to initialize the database
/// </summary>
public class DatabaseSetupTests
{
    private readonly string _dbPath;

    public DatabaseSetupTests()
    {
        _dbPath = Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data")),
            "ainewshub.db");
    }

    [Fact]
    public async Task Setup_Database_With_Seed_Data()
    {
        // Arrange
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");

        using var context = new AppDbContext(optionsBuilder.Options);

        // Act - Create database
        context.Database.EnsureCreated();

        // Seed sources if needed
        if (!await context.Sources.AnyAsync())
        {
            context.Sources.AddRange(
                new Source { Name = "Anthropic Blog", Url = "https://www.anthropic.com/news", CrawlerType = "Playwright", CrawlOffsetMinutes = 0 },
                new Source { Name = "Anthropic Research", Url = "https://www.anthropic.com/research", CrawlerType = "Playwright", CrawlOffsetMinutes = 1 },
                new Source { Name = "OpenAI Blog", Url = "https://openai.com/blog", CrawlerType = "Playwright", CrawlOffsetMinutes = 2 },
                new Source { Name = "Google AI Blog", Url = "https://blog.google/technology/ai", CrawlerType = "Playwright", CrawlOffsetMinutes = 3 },
                new Source { Name = "DeepMind Blog", Url = "https://deepmind.google/blog", CrawlerType = "Playwright", CrawlOffsetMinutes = 4 },
                new Source { Name = "Microsoft AI Blog", Url = "https://blogs.microsoft.com/ai", CrawlerType = "Playwright", CrawlOffsetMinutes = 5 },
                new Source { Name = "r/MachineLearning", Url = "https://reddit.com/r/MachineLearning", CrawlerType = "RedditApi", CrawlOffsetMinutes = 6 },
                new Source { Name = "r/LocalLLaMA", Url = "https://reddit.com/r/LocalLLaMA", CrawlerType = "RedditApi", CrawlOffsetMinutes = 7 },
                new Source { Name = "Hacker News", Url = "https://news.ycombinator.com", CrawlerType = "HackerNewsApi", CrawlOffsetMinutes = 8 }
            );
            await context.SaveChangesAsync();
        }

        // Seed tags if needed
        if (!await context.Tags.AnyAsync())
        {
            var tags = new[]
            {
                // Core AI concepts
                "LLM", "GPT", "Claude", "Gemini", "Llama", "Mistral",
                // Techniques
                "RAG", "Fine-tuning", "RLHF", "DPO", "Prompt Engineering",
                "Chain of Thought", "Few-shot Learning", "Zero-shot Learning",
                // Architecture
                "Transformer", "Attention", "Embedding", "Tokenization",
                "Multimodal", "Vision", "Audio", "Text-to-Image",
                // Tools & Platforms
                "MCP", "Tool Use", "Function Calling", "Agents", "Agentic AI",
                "Claude Code", "GitHub Copilot", "Cursor",
                // Safety & Ethics
                "AI Safety", "Alignment", "Constitutional AI", "RLAIF",
                "Hallucination", "Bias", "Red Teaming",
                // Infrastructure
                "Inference", "Quantization", "Distillation", "Deployment",
                "API", "SDK", "Cloud", "Edge AI",
                // Research
                "Benchmark", "Evaluation", "Scaling Laws", "Emergent Abilities",
                "Reasoning", "Math", "Coding", "Summarization"
            };

            foreach (var tagName in tags)
            {
                context.Tags.Add(new Tag { Name = tagName });
            }
            await context.SaveChangesAsync();
        }

        // Assert
        File.Exists(_dbPath).Should().BeTrue("database file should exist");

        var sourcesCount = await context.Sources.CountAsync();
        var tagsCount = await context.Tags.CountAsync();

        sourcesCount.Should().Be(9, "should have 9 sources");
        tagsCount.Should().BeGreaterThan(40, "should have many tags");

        Console.WriteLine($"\n=== Database Setup Complete ===");
        Console.WriteLine($"Database path: {_dbPath}");
        Console.WriteLine($"Sources: {sourcesCount}");
        Console.WriteLine($"Tags: {tagsCount}");
    }
}

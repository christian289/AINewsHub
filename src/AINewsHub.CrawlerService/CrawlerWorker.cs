using AINewsHub.Core.Entities;
using AINewsHub.Core.Interfaces;
using AINewsHub.Infrastructure.Crawlers;
using AINewsHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AINewsHub.CrawlerService;

public class CrawlerWorker : BackgroundService
{
    private readonly ILogger<CrawlerWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);

    public CrawlerWorker(ILogger<CrawlerWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Crawler Worker starting at: {time}", DateTimeOffset.Now);

        // Initial delay to let other services start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CrawlAllSourcesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during crawl cycle");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task CrawlAllSourcesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var articleService = scope.ServiceProvider.GetRequiredService<IArticleService>();

        var sources = await context.Sources
            .Where(s => s.IsActive)
            .OrderBy(s => s.CrawlOffsetMinutes)
            .ToListAsync(stoppingToken);

        _logger.LogInformation("Starting crawl cycle for {count} active sources", sources.Count);

        foreach (var source in sources)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            // Check if it's time to crawl this source based on offset
            var minuteOfHour = DateTime.UtcNow.Minute % 10;
            if (minuteOfHour != source.CrawlOffsetMinutes % 10 && sources.Count > 1)
            {
                _logger.LogDebug("Skipping {source} - not scheduled for this interval", source.Name);
                continue;
            }

            try
            {
                _logger.LogInformation("Crawling source: {name} ({type})", source.Name, source.CrawlerType);

                var articles = await CrawlSourceAsync(scope.ServiceProvider, source, stoppingToken);
                var newArticles = 0;

                foreach (var article in articles)
                {
                    if (await articleService.ArticleExistsAsync(article.Url))
                    {
                        _logger.LogDebug("Article already exists: {url}", article.Url);
                        continue;
                    }

                    await articleService.CreateArticleAsync(article);
                    newArticles++;
                    _logger.LogInformation("New article saved: {title}", article.Title);
                }

                // Update last crawled time
                source.LastCrawledAt = DateTime.UtcNow;
                await context.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Crawl complete for {name}: {new} new articles", source.Name, newArticles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling source: {name}", source.Name);
            }

            // Small delay between sources to avoid overwhelming
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task<IEnumerable<Article>> CrawlSourceAsync(
        IServiceProvider services,
        Source source,
        CancellationToken stoppingToken)
    {
        return source.CrawlerType switch
        {
            "Playwright" => await CrawlWithPlaywrightAsync(services, source, stoppingToken),
            "RedditApi" => await CrawlWithRedditAsync(services, source, stoppingToken),
            "HackerNewsApi" => await CrawlWithHackerNewsAsync(services, source, stoppingToken),
            _ => throw new NotSupportedException($"Unknown crawler type: {source.CrawlerType}")
        };
    }

    private async Task<IEnumerable<Article>> CrawlWithPlaywrightAsync(
        IServiceProvider services,
        Source source,
        CancellationToken stoppingToken)
    {
        var crawler = services.GetRequiredService<PlaywrightCrawler>();
        return await crawler.CrawlSourceAsync(source);
    }

    private async Task<IEnumerable<Article>> CrawlWithRedditAsync(
        IServiceProvider services,
        Source source,
        CancellationToken stoppingToken)
    {
        var client = services.GetRequiredService<RedditApiClient>();
        return await client.CrawlSourceAsync(source);
    }

    private async Task<IEnumerable<Article>> CrawlWithHackerNewsAsync(
        IServiceProvider services,
        Source source,
        CancellationToken stoppingToken)
    {
        var client = services.GetRequiredService<HackerNewsApiClient>();
        return await client.CrawlSourceAsync(source);
    }
}

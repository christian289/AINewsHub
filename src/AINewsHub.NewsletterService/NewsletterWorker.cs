using AINewsHub.Core.Entities;
using AINewsHub.Core.Enums;
using AINewsHub.Core.Interfaces;
using AINewsHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AINewsHub.NewsletterService;

public class NewsletterWorker : BackgroundService
{
    private readonly ILogger<NewsletterWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public NewsletterWorker(ILogger<NewsletterWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Newsletter Worker starting at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextMidnight = now.Date.AddDays(1);
                var delay = nextMidnight - now;

                _logger.LogInformation("Next newsletter generation at: {time} (in {delay})", nextMidnight, delay);

                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await GenerateNewslettersAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in newsletter worker");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task GenerateNewslettersAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting daily newsletter generation at: {time}", DateTimeOffset.Now);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var articleService = scope.ServiceProvider.GetRequiredService<IArticleService>();

        // Get articles by level (2 per level for newsletter)
        foreach (var level in Enum.GetValues<UserLevel>())
        {
            var articles = await articleService.GetArticlesByLevelAsync(level, 2);
            _logger.LogInformation("Selected {count} articles for {level} level newsletter",
                articles.Count(), level);
        }

        _logger.LogInformation("Newsletter generation complete");
    }
}

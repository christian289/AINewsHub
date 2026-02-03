using Microsoft.Playwright;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Interfaces;

namespace AINewsHub.Infrastructure.Crawlers;

/// <summary>
/// Playwright-based web crawler for blog sites
/// Used for: Anthropic, OpenAI, Google, Microsoft blogs
/// </summary>
public class PlaywrightCrawler : ICrawlerService, IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<IEnumerable<Article>> CrawlSourceAsync(Source source)
    {
        await EnsureInitializedAsync();

        var articles = new List<Article>();

        try
        {
            var page = await _browser!.NewPageAsync();

            try
            {
                await page.GotoAsync(source.Url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 30000
                });

                // Generic blog article extraction - can be customized per source
                var articleLinks = await ExtractArticleLinksAsync(page, source);

                foreach (var link in articleLinks.Take(10)) // Limit to 10 articles per crawl
                {
                    try
                    {
                        var article = await CrawlArticleAsync(page, link, source);
                        if (article != null)
                        {
                            articles.Add(article);
                        }
                    }
                    catch (Exception)
                    {
                        // Log and continue with next article
                    }
                }
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        catch (Exception)
        {
            // Log crawl failure
        }

        return articles;
    }

    private async Task<IEnumerable<string>> ExtractArticleLinksAsync(IPage page, Source source)
    {
        var links = new List<string>();

        // Common blog article selectors
        var selectors = new[]
        {
            "article a[href]",
            ".post a[href]",
            ".blog-post a[href]",
            "[class*='article'] a[href]",
            "[class*='post'] a[href]",
            "main a[href]"
        };

        foreach (var selector in selectors)
        {
            try
            {
                var elements = await page.QuerySelectorAllAsync(selector);
                foreach (var element in elements)
                {
                    var href = await element.GetAttributeAsync("href");
                    if (!string.IsNullOrEmpty(href))
                    {
                        // Normalize URL
                        if (href.StartsWith("/"))
                        {
                            var uri = new Uri(source.Url);
                            href = $"{uri.Scheme}://{uri.Host}{href}";
                        }

                        if (Uri.TryCreate(href, UriKind.Absolute, out var fullUri) &&
                            fullUri.Host.Contains(new Uri(source.Url).Host))
                        {
                            links.Add(href);
                        }
                    }
                }

                if (links.Count > 0) break; // Use first selector that works
            }
            catch
            {
                // Try next selector
            }
        }

        return links.Distinct();
    }

    private async Task<Article?> CrawlArticleAsync(IPage page, string url, Source source)
    {
        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 20000
        });

        // Extract title
        var title = await ExtractTitleAsync(page);
        if (string.IsNullOrEmpty(title)) return null;

        // Extract content
        var content = await ExtractContentAsync(page);
        if (string.IsNullOrEmpty(content)) return null;

        // Extract publish date
        var publishDate = await ExtractPublishDateAsync(page);

        return new Article
        {
            Title = title,
            Url = url,
            Content = content,
            Summary = content.Length > 500 ? content[..500] + "..." : content,
            SourceId = source.Id,
            PublishedAt = publishDate ?? DateTime.UtcNow,
            CrawledAt = DateTime.UtcNow,
            WordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            IsProcessed = false
        };
    }

    private async Task<string?> ExtractTitleAsync(IPage page)
    {
        var selectors = new[] { "h1", "article h1", ".post-title", "[class*='title']" };

        foreach (var selector in selectors)
        {
            try
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var text = await element.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text.Trim();
                }
            }
            catch
            {
                // Try next selector
            }
        }

        // Fallback to page title
        return await page.TitleAsync();
    }

    private async Task<string?> ExtractContentAsync(IPage page)
    {
        var selectors = new[] { "article", ".post-content", ".content", "main", "[class*='article-body']" };

        foreach (var selector in selectors)
        {
            try
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var text = await element.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(text) && text.Length > 100)
                        return text.Trim();
                }
            }
            catch
            {
                // Try next selector
            }
        }

        return null;
    }

    private async Task<DateTime?> ExtractPublishDateAsync(IPage page)
    {
        var selectors = new[] { "time[datetime]", "[class*='date']", "[class*='published']", "meta[property='article:published_time']" };

        foreach (var selector in selectors)
        {
            try
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    string? dateStr = null;

                    if (selector.Contains("meta"))
                    {
                        dateStr = await element.GetAttributeAsync("content");
                    }
                    else if (selector.Contains("time"))
                    {
                        dateStr = await element.GetAttributeAsync("datetime");
                    }
                    else
                    {
                        dateStr = await element.InnerTextAsync();
                    }

                    if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
                        return date.ToUniversalTime();
                }
            }
            catch
            {
                // Try next selector
            }
        }

        return null;
    }

    public async Task<bool> ProcessArticleAsync(Article article)
    {
        // Mark as processed - actual processing (tagging, summarization) would happen here
        article.IsProcessed = true;
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
        _initLock.Dispose();
    }
}

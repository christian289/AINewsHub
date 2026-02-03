using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Interfaces;

namespace AINewsHub.Infrastructure.Crawlers;

/// <summary>
/// Hacker News API client
/// API docs: https://github.com/HackerNews/API
/// </summary>
public class HackerNewsApiClient : ICrawlerService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://hacker-news.firebaseio.com/v0";

    // AI-related keywords to filter stories
    private static readonly string[] AiKeywords =
    [
        "ai", "artificial intelligence", "machine learning", "ml", "deep learning",
        "llm", "gpt", "claude", "chatgpt", "openai", "anthropic", "google ai",
        "neural network", "transformer", "diffusion", "stable diffusion", "midjourney",
        "language model", "generative ai", "gen ai", "rag", "vector database",
        "embedding", "fine-tuning", "prompt", "agent", "llama", "mistral", "gemini"
    ];

    public HackerNewsApiClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl)
        };
    }

    public HackerNewsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (_httpClient.BaseAddress == null)
            _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<IEnumerable<Article>> CrawlSourceAsync(Source source)
    {
        var articles = new List<Article>();

        try
        {
            // Get top stories
            var storyIds = await _httpClient.GetFromJsonAsync<int[]>("/topstories.json");
            if (storyIds == null) return articles;

            // Fetch top 100 stories and filter for AI-related content
            var tasks = storyIds.Take(100).Select(id => FetchStoryAsync(id, source));
            var stories = await Task.WhenAll(tasks);

            articles.AddRange(stories.Where(a => a != null)!);
        }
        catch (Exception)
        {
            // Log error
        }

        return articles;
    }

    private async Task<Article?> FetchStoryAsync(int storyId, Source source)
    {
        try
        {
            var story = await _httpClient.GetFromJsonAsync<HackerNewsStory>($"/item/{storyId}.json");
            if (story == null || string.IsNullOrEmpty(story.Title))
                return null;

            // Filter for AI-related stories
            var titleLower = story.Title.ToLowerInvariant();
            var textLower = (story.Text ?? "").ToLowerInvariant();

            var isAiRelated = AiKeywords.Any(keyword =>
                titleLower.Contains(keyword) || textLower.Contains(keyword));

            if (!isAiRelated)
                return null;

            var content = story.Text ?? $"Link: {story.Url}";
            var articleUrl = !string.IsNullOrEmpty(story.Url)
                ? story.Url
                : $"https://news.ycombinator.com/item?id={storyId}";

            return new Article
            {
                Title = story.Title,
                Url = articleUrl,
                Content = content,
                Summary = content.Length > 500 ? content[..500] + "..." : content,
                SourceId = source.Id,
                PublishedAt = DateTimeOffset.FromUnixTimeSeconds(story.Time).UtcDateTime,
                CrawledAt = DateTime.UtcNow,
                WordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                IsProcessed = false
            };
        }
        catch
        {
            return null;
        }
    }

    public Task<bool> ProcessArticleAsync(Article article)
    {
        article.IsProcessed = true;
        return Task.FromResult(true);
    }

    private class HackerNewsStory
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("time")]
        public long Time { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("by")]
        public string? By { get; set; }

        [JsonPropertyName("descendants")]
        public int Descendants { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
}

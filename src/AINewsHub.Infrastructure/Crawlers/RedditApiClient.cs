using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Interfaces;

namespace AINewsHub.Infrastructure.Crawlers;

/// <summary>
/// Reddit OAuth API client for r/MachineLearning, r/LocalLLaMA
/// </summary>
public class RedditApiClient : ICrawlerService
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _userAgent;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public RedditApiClient(string clientId, string clientSecret, string userAgent = "AINewsHub/1.0")
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _userAgent = userAgent;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);
    }

    private async Task EnsureAuthenticatedAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
            return;

        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.reddit.com/api/v1/access_token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" }
        });

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<RedditTokenResponse>();
        if (tokenResponse != null)
        {
            _accessToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60); // 60s buffer
        }
    }

    public async Task<IEnumerable<Article>> CrawlSourceAsync(Source source)
    {
        var articles = new List<Article>();

        try
        {
            await EnsureAuthenticatedAsync();

            // Extract subreddit from URL (e.g., "https://reddit.com/r/MachineLearning" -> "MachineLearning")
            var subreddit = ExtractSubreddit(source.Url);
            if (string.IsNullOrEmpty(subreddit))
                return articles;

            var url = $"https://oauth.reddit.com/r/{subreddit}/hot?limit=25";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var listing = await response.Content.ReadFromJsonAsync<RedditListing>();
            if (listing?.Data?.Children == null)
                return articles;

            foreach (var child in listing.Data.Children)
            {
                var post = child.Data;
                if (post == null) continue;

                // Filter for text posts with substantial content
                if (string.IsNullOrWhiteSpace(post.SelfText) && string.IsNullOrWhiteSpace(post.Url))
                    continue;

                var content = !string.IsNullOrWhiteSpace(post.SelfText)
                    ? post.SelfText
                    : $"Link: {post.Url}";

                var article = new Article
                {
                    Title = post.Title ?? "Untitled",
                    Url = $"https://reddit.com{post.Permalink}",
                    Content = content,
                    Summary = content.Length > 500 ? content[..500] + "..." : content,
                    SourceId = source.Id,
                    PublishedAt = DateTimeOffset.FromUnixTimeSeconds((long)post.CreatedUtc).UtcDateTime,
                    CrawledAt = DateTime.UtcNow,
                    WordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                    IsProcessed = false
                };

                articles.Add(article);
            }
        }
        catch (Exception)
        {
            // Log error and return what we have
        }

        return articles;
    }

    public Task<bool> ProcessArticleAsync(Article article)
    {
        article.IsProcessed = true;
        return Task.FromResult(true);
    }

    private static string? ExtractSubreddit(string url)
    {
        try
        {
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Equals("r", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
                {
                    return segments[i + 1];
                }
            }
        }
        catch
        {
            // Invalid URL
        }

        return null;
    }

    private class RedditTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private class RedditListing
    {
        [JsonPropertyName("data")]
        public RedditListingData? Data { get; set; }
    }

    private class RedditListingData
    {
        [JsonPropertyName("children")]
        public List<RedditChild>? Children { get; set; }
    }

    private class RedditChild
    {
        [JsonPropertyName("data")]
        public RedditPost? Data { get; set; }
    }

    private class RedditPost
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("selftext")]
        public string? SelfText { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("permalink")]
        public string? Permalink { get; set; }

        [JsonPropertyName("created_utc")]
        public double CreatedUtc { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("num_comments")]
        public int NumComments { get; set; }
    }
}

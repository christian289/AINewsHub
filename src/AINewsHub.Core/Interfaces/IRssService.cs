namespace AINewsHub.Core.Interfaces;

using AINewsHub.Core.Entities;

public interface IRssService
{
    Task<string> GenerateRssFeedAsync(long snowflakeId);
    Task<IEnumerable<Article>> GetPersonalizedArticlesAsync(int userId, int count = 10);
    long? ParseSnowflakeIdFromUrl(string url);
}

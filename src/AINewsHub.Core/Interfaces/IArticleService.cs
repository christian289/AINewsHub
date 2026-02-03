namespace AINewsHub.Core.Interfaces;

using AINewsHub.Core.Entities;
using AINewsHub.Core.Enums;

public interface IArticleService
{
    Task<Article?> GetArticleByIdAsync(int id);
    Task<Article?> GetArticleByUrlAsync(string url);
    Task<IEnumerable<Article>> GetRecentArticlesAsync(int count = 20);
    Task<Article> CreateArticleAsync(Article article);
    Task<bool> ArticleExistsAsync(string url);
    Task<IEnumerable<Article>> GetArticlesByLevelAsync(UserLevel level, int count);
}

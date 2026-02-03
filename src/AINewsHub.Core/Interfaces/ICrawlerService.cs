namespace AINewsHub.Core.Interfaces;

using AINewsHub.Core.Entities;

public interface ICrawlerService
{
    Task<IEnumerable<Article>> CrawlSourceAsync(Source source);
    Task<bool> ProcessArticleAsync(Article article);
}

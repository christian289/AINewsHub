using Microsoft.EntityFrameworkCore;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Enums;
using AINewsHub.Core.Interfaces;
using AINewsHub.Infrastructure.Data;

namespace AINewsHub.Infrastructure.Services;

public class ArticleService : IArticleService
{
    private readonly AppDbContext _context;

    public ArticleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Article?> GetArticleByIdAsync(int id)
    {
        return await _context.Articles
            .Include(a => a.Source)
            .Include(a => a.ArticleTags)
            .ThenInclude(at => at.Tag)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Article?> GetArticleByUrlAsync(string url)
    {
        return await _context.Articles
            .Include(a => a.Source)
            .FirstOrDefaultAsync(a => a.Url == url);
    }

    public async Task<IEnumerable<Article>> GetRecentArticlesAsync(int count = 20)
    {
        return await _context.Articles
            .Include(a => a.Source)
            .Include(a => a.ArticleTags)
            .ThenInclude(at => at.Tag)
            .OrderByDescending(a => a.PublishedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<Article> CreateArticleAsync(Article article)
    {
        _context.Articles.Add(article);
        await _context.SaveChangesAsync();
        return article;
    }

    public async Task<bool> ArticleExistsAsync(string url)
    {
        return await _context.Articles.AnyAsync(a => a.Url == url);
    }

    public async Task<IEnumerable<Article>> GetArticlesByLevelAsync(UserLevel level, int count)
    {
        // Articles are tagged and filtered based on complexity
        // For now, return recent articles - this can be enhanced with level-based filtering
        return await _context.Articles
            .Include(a => a.Source)
            .Include(a => a.ArticleTags)
            .ThenInclude(at => at.Tag)
            .Where(a => a.IsProcessed)
            .OrderByDescending(a => a.PublishedAt)
            .Take(count)
            .ToListAsync();
    }
}

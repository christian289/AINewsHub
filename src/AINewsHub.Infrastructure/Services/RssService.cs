using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Enums;
using AINewsHub.Core.Interfaces;
using AINewsHub.Infrastructure.Data;

namespace AINewsHub.Infrastructure.Services;

public class RssService : IRssService
{
    private readonly AppDbContext _context;
    private readonly IUserService _userService;

    public RssService(AppDbContext context, IUserService userService)
    {
        _context = context;
        _userService = userService;
    }

    public async Task<string> GenerateRssFeedAsync(long snowflakeId)
    {
        var user = await _userService.GetUserBySnowflakeIdAsync(snowflakeId);
        if (user == null)
            throw new KeyNotFoundException($"User with Snowflake ID {snowflakeId} not found");

        var articles = await GetPersonalizedArticlesAsync(user.Id);

        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("rss");
        writer.WriteAttributeString("version", "2.0");

        writer.WriteStartElement("channel");
        writer.WriteElementString("title", "AINewsHub - Personalized AI News");
        writer.WriteElementString("link", $"https://ainewshub.local/rss/{snowflakeId}");
        writer.WriteElementString("description", "Your personalized AI news feed");
        writer.WriteElementString("language", "en-us");
        writer.WriteElementString("lastBuildDate", DateTime.UtcNow.ToString("R"));

        foreach (var article in articles)
        {
            writer.WriteStartElement("item");
            writer.WriteElementString("title", article.Title);
            writer.WriteElementString("link", article.Url);
            writer.WriteElementString("description", article.Summary);
            writer.WriteElementString("pubDate", article.PublishedAt.ToString("R"));
            writer.WriteElementString("guid", article.Url);

            // Add tags as categories
            foreach (var articleTag in article.ArticleTags)
            {
                writer.WriteElementString("category", articleTag.Tag.Name);
            }

            writer.WriteEndElement(); // item
        }

        writer.WriteEndElement(); // channel
        writer.WriteEndElement(); // rss
        writer.WriteEndDocument();

        return sb.ToString();
    }

    public async Task<IEnumerable<Article>> GetPersonalizedArticlesAsync(int userId, int count = 10)
    {
        var user = await _context.Users
            .Include(u => u.TagPreferences)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return [];

        var mustIncludeTagIds = user.TagPreferences
            .Where(p => p.PreferenceType == TagPreferenceType.MustInclude)
            .Select(p => p.TagId)
            .ToList();

        var excludeTagIds = user.TagPreferences
            .Where(p => p.PreferenceType == TagPreferenceType.Exclude)
            .Select(p => p.TagId)
            .ToList();

        var query = _context.Articles
            .Include(a => a.Source)
            .Include(a => a.ArticleTags)
            .ThenInclude(at => at.Tag)
            .Where(a => a.IsProcessed);

        // Exclude articles with excluded tags
        if (excludeTagIds.Count > 0)
        {
            query = query.Where(a => !a.ArticleTags.Any(at => excludeTagIds.Contains(at.TagId)));
        }

        // Prioritize articles with must-include tags
        if (mustIncludeTagIds.Count > 0)
        {
            query = query.OrderByDescending(a =>
                a.ArticleTags.Count(at => mustIncludeTagIds.Contains(at.TagId)))
                .ThenByDescending(a => a.PublishedAt);
        }
        else
        {
            query = query.OrderByDescending(a => a.PublishedAt);
        }

        return await query.Take(count).ToListAsync();
    }

    public long? ParseSnowflakeIdFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var segments = url.Split('/');
        var lastSegment = segments.LastOrDefault(s => !string.IsNullOrWhiteSpace(s));

        if (long.TryParse(lastSegment, out var snowflakeId))
            return snowflakeId;

        return null;
    }
}

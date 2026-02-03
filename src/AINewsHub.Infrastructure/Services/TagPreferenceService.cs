using Microsoft.EntityFrameworkCore;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Enums;
using AINewsHub.Core.Interfaces;
using AINewsHub.Infrastructure.Data;

namespace AINewsHub.Infrastructure.Services;

public class TagPreferenceService : ITagPreferenceService
{
    private readonly AppDbContext _context;

    public TagPreferenceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<UserTagPreference>> GetUserPreferencesAsync(int userId)
    {
        return await _context.UserTagPreferences
            .Include(p => p.Tag)
            .Where(p => p.UserId == userId)
            .ToListAsync();
    }

    public async Task<bool> SetPreferencesAsync(int userId, IEnumerable<int> mustIncludeTagIds, IEnumerable<int> excludeTagIds)
    {
        var mustIncludeList = mustIncludeTagIds.ToList();
        var excludeList = excludeTagIds.ToList();

        // Validate limits
        if (mustIncludeList.Count > ITagPreferenceService.MaxMustIncludeTags)
            throw new ArgumentException($"Maximum {ITagPreferenceService.MaxMustIncludeTags} must-include tags allowed");

        if (excludeList.Count > ITagPreferenceService.MaxExcludeTags)
            throw new ArgumentException($"Maximum {ITagPreferenceService.MaxExcludeTags} exclude tags allowed");

        // Check for overlapping tags
        if (mustIncludeList.Intersect(excludeList).Any())
            throw new ArgumentException("A tag cannot be both must-include and exclude");

        // Remove existing preferences
        var existingPreferences = await _context.UserTagPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync();
        _context.UserTagPreferences.RemoveRange(existingPreferences);

        // Add new must-include preferences
        foreach (var tagId in mustIncludeList)
        {
            _context.UserTagPreferences.Add(new UserTagPreference
            {
                UserId = userId,
                TagId = tagId,
                PreferenceType = TagPreferenceType.MustInclude,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Add new exclude preferences
        foreach (var tagId in excludeList)
        {
            _context.UserTagPreferences.Add(new UserTagPreference
            {
                UserId = userId,
                TagId = tagId,
                PreferenceType = TagPreferenceType.Exclude,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Tag>> GetAllTagsAsync()
    {
        return await _context.Tags.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<IEnumerable<Tag>> GetMustIncludeTagsAsync(int userId)
    {
        return await _context.UserTagPreferences
            .Include(p => p.Tag)
            .Where(p => p.UserId == userId && p.PreferenceType == TagPreferenceType.MustInclude)
            .Select(p => p.Tag)
            .ToListAsync();
    }

    public async Task<IEnumerable<Tag>> GetExcludeTagsAsync(int userId)
    {
        return await _context.UserTagPreferences
            .Include(p => p.Tag)
            .Where(p => p.UserId == userId && p.PreferenceType == TagPreferenceType.Exclude)
            .Select(p => p.Tag)
            .ToListAsync();
    }
}

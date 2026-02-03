using Microsoft.EntityFrameworkCore;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Interfaces;
using AINewsHub.Infrastructure.Data;

namespace AINewsHub.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly ISnowflakeIdGenerator _snowflakeGenerator;

    public UserService(AppDbContext context, ISnowflakeIdGenerator snowflakeGenerator)
    {
        _context = context;
        _snowflakeGenerator = snowflakeGenerator;
    }

    public async Task<User> CreateUserAsync()
    {
        var user = new User
        {
            SnowflakeId = _snowflakeGenerator.NextId(),
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<User?> GetUserBySnowflakeIdAsync(long snowflakeId)
    {
        return await _context.Users
            .Include(u => u.TagPreferences)
            .ThenInclude(tp => tp.Tag)
            .FirstOrDefaultAsync(u => u.SnowflakeId == snowflakeId);
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await _context.Users
            .Include(u => u.TagPreferences)
            .ThenInclude(tp => tp.Tag)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> CanRetestAsync(long snowflakeId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.SnowflakeId == snowflakeId);
        if (user == null) return false;
        if (user.LastTestDate == null) return true;

        // Check if 7 days have passed
        return (DateTime.UtcNow - user.LastTestDate.Value).TotalDays >= 7;
    }

    public async Task UpdateLastActiveAsync(long snowflakeId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.SnowflakeId == snowflakeId);
        if (user != null)
        {
            user.LastActiveAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<User> RecoverUserFromRssUrlAsync(string rssUrl)
    {
        // Parse Snowflake ID from URL: /rss/{snowflakeId}
        var segments = rssUrl.Split('/');
        var lastSegment = segments.LastOrDefault(s => !string.IsNullOrWhiteSpace(s));

        if (string.IsNullOrEmpty(lastSegment) || !long.TryParse(lastSegment, out var snowflakeId))
            throw new ArgumentException("Invalid RSS URL format. Expected /rss/{snowflakeId}");

        var user = await GetUserBySnowflakeIdAsync(snowflakeId);
        if (user == null)
            throw new KeyNotFoundException($"User with Snowflake ID {snowflakeId} not found");

        return user;
    }
}

namespace AINewsHub.Core.Interfaces;

using AINewsHub.Core.Entities;

public interface IUserService
{
    Task<User> CreateUserAsync();
    Task<User?> GetUserBySnowflakeIdAsync(long snowflakeId);
    Task<User?> GetUserByIdAsync(int id);
    Task<bool> CanRetestAsync(long snowflakeId);
    Task UpdateLastActiveAsync(long snowflakeId);
    Task<User> RecoverUserFromRssUrlAsync(string rssUrl);
}

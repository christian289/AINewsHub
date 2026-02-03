namespace AINewsHub.Core.Interfaces;

using AINewsHub.Core.Entities;

public interface ITagPreferenceService
{
    Task<IEnumerable<UserTagPreference>> GetUserPreferencesAsync(int userId);
    Task<bool> SetPreferencesAsync(int userId, IEnumerable<int> mustIncludeTagIds, IEnumerable<int> excludeTagIds);
    Task<IEnumerable<Tag>> GetAllTagsAsync();
    Task<IEnumerable<Tag>> GetMustIncludeTagsAsync(int userId);
    Task<IEnumerable<Tag>> GetExcludeTagsAsync(int userId);

    // Validation constants
    const int MaxMustIncludeTags = 5;
    const int MaxExcludeTags = 10;
}

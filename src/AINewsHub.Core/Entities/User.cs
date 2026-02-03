namespace AINewsHub.Core.Entities;

using AINewsHub.Core.Enums;

public class User
{
    public int Id { get; set; }

    /// <summary>
    /// Snowflake ID - 64-bit unique identifier
    /// 충돌 없는 분산 고유 ID
    /// </summary>
    public long SnowflakeId { get; set; }

    public UserLevel Level { get; set; } = UserLevel.Beginner;
    public DateTime? LastTestDate { get; set; }
    public int TestCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<UserTagPreference> TagPreferences { get; set; } = [];
    public ICollection<TestHistory> TestHistories { get; set; } = [];
}

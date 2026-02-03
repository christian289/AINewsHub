namespace AINewsHub.Core.Entities;

using AINewsHub.Core.Enums;

public class UserTagPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TagId { get; set; }
    public TagPreferenceType PreferenceType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

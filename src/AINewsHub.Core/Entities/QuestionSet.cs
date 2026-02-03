namespace AINewsHub.Core.Entities;

public class QuestionSet
{
    public int Id { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAt { get; set; }
    public string? SourceKeywords { get; set; } // JSON array of keywords used to generate

    // Navigation
    public ICollection<Question> Questions { get; set; } = [];
    public ICollection<TestHistory> TestHistories { get; set; } = [];
}

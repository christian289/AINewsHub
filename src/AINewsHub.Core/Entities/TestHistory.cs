namespace AINewsHub.Core.Entities;

using AINewsHub.Core.Enums;

public class TestHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime TestDate { get; set; } = DateTime.UtcNow;
    public UserLevel ResultLevel { get; set; }
    public int QuestionSetId { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; } = 8;

    // Navigation
    public User User { get; set; } = null!;
    public QuestionSet QuestionSet { get; set; } = null!;
}

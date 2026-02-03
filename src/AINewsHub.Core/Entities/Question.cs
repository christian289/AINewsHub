namespace AINewsHub.Core.Entities;

using AINewsHub.Core.Enums;

public class Question
{
    public int Id { get; set; }
    public int QuestionSetId { get; set; }
    public int OrderIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "[]"; // JSON array of 4 options
    public int CorrectOptionIndex { get; set; }
    public UserLevel TargetLevel { get; set; } // Difficulty targeting
    public string? SourceKeyword { get; set; } // Forum keyword this was based on

    // Navigation
    public QuestionSet QuestionSet { get; set; } = null!;
}

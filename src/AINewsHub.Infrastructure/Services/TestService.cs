using Microsoft.EntityFrameworkCore;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Enums;
using AINewsHub.Core.Interfaces;
using AINewsHub.Infrastructure.Data;

namespace AINewsHub.Infrastructure.Services;

public class TestService : ITestService
{
    private readonly AppDbContext _context;

    public TestService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<QuestionSet?> GetActiveQuestionSetAsync()
    {
        return await _context.QuestionSets
            .Include(qs => qs.Questions.OrderBy(q => q.OrderIndex))
            .FirstOrDefaultAsync(qs => qs.IsActive);
    }

    public async Task<IEnumerable<Question>> GetQuestionsAsync(int questionSetId)
    {
        return await _context.Questions
            .Where(q => q.QuestionSetId == questionSetId)
            .OrderBy(q => q.OrderIndex)
            .ToListAsync();
    }

    public async Task<(UserLevel Level, int CorrectAnswers)> SubmitTestAsync(
        int userId,
        int questionSetId,
        IDictionary<int, int> answers)
    {
        var questions = await _context.Questions
            .Where(q => q.QuestionSetId == questionSetId)
            .ToListAsync();

        var correctAnswers = questions.Count(q =>
            answers.TryGetValue(q.Id, out var answer) && answer == q.CorrectOptionIndex);

        // Level determination based on correct answers (out of 8)
        // 0-3: Beginner, 4-5: Intermediate, 6-8: Advanced
        var level = correctAnswers switch
        {
            >= 6 => UserLevel.Advanced,
            >= 4 => UserLevel.Intermediate,
            _ => UserLevel.Beginner
        };

        // Update user
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.Level = level;
            user.LastTestDate = DateTime.UtcNow;
            user.TestCount++;
        }

        // Record test history
        var history = new TestHistory
        {
            UserId = userId,
            QuestionSetId = questionSetId,
            TestDate = DateTime.UtcNow,
            ResultLevel = level,
            CorrectAnswers = correctAnswers,
            TotalQuestions = questions.Count
        };
        _context.TestHistories.Add(history);

        await _context.SaveChangesAsync();

        return (level, correctAnswers);
    }

    public async Task<bool> ActivateQuestionSetAsync(int questionSetId)
    {
        // Deactivate all other question sets
        var allSets = await _context.QuestionSets.ToListAsync();
        foreach (var set in allSets)
        {
            set.IsActive = set.Id == questionSetId;
            if (set.Id == questionSetId)
                set.ActivatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<QuestionSet> CreateQuestionSetAsync(IEnumerable<Question> questions, string? sourceKeywords)
    {
        var maxVersion = await _context.QuestionSets.MaxAsync(qs => (int?)qs.Version) ?? 0;

        var questionSet = new QuestionSet
        {
            Version = maxVersion + 1,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            SourceKeywords = sourceKeywords
        };

        _context.QuestionSets.Add(questionSet);
        await _context.SaveChangesAsync();

        var orderIndex = 0;
        foreach (var question in questions)
        {
            question.QuestionSetId = questionSet.Id;
            question.OrderIndex = orderIndex++;
            _context.Questions.Add(question);
        }

        await _context.SaveChangesAsync();

        return questionSet;
    }
}

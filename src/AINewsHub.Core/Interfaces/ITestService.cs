namespace AINewsHub.Core.Interfaces;

using AINewsHub.Core.Entities;
using AINewsHub.Core.Enums;

public interface ITestService
{
    Task<QuestionSet?> GetActiveQuestionSetAsync();
    Task<IEnumerable<Question>> GetQuestionsAsync(int questionSetId);
    Task<(UserLevel Level, int CorrectAnswers)> SubmitTestAsync(int userId, int questionSetId, IDictionary<int, int> answers);
    Task<bool> ActivateQuestionSetAsync(int questionSetId);
    Task<QuestionSet> CreateQuestionSetAsync(IEnumerable<Question> questions, string? sourceKeywords);
}

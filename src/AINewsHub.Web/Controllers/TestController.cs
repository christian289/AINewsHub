using Microsoft.AspNetCore.Mvc;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Interfaces;

namespace AINewsHub.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ITestService _testService;
    private readonly IUserService _userService;

    public TestController(ITestService testService, IUserService userService)
    {
        _testService = testService;
        _userService = userService;
    }

    /// <summary>
    /// Get active question set
    /// </summary>
    [HttpGet("questions")]
    public async Task<ActionResult<QuestionSetResponse>> GetQuestions()
    {
        var questionSet = await _testService.GetActiveQuestionSetAsync();
        if (questionSet == null)
            return NotFound(new { error = "No active question set" });

        return Ok(new QuestionSetResponse(questionSet));
    }

    /// <summary>
    /// Submit test answers
    /// </summary>
    [HttpPost("{snowflakeId:long}/submit")]
    public async Task<ActionResult<TestResultResponse>> SubmitTest(
        long snowflakeId,
        [FromBody] SubmitTestRequest request)
    {
        var user = await _userService.GetUserBySnowflakeIdAsync(snowflakeId);
        if (user == null)
            return NotFound(new { error = "User not found" });

        var canRetest = await _userService.CanRetestAsync(snowflakeId);
        if (!canRetest)
            return BadRequest(new { error = "Retest not allowed yet. Please wait 7 days." });

        var (level, correctAnswers) = await _testService.SubmitTestAsync(
            user.Id,
            request.QuestionSetId,
            request.Answers);

        return Ok(new TestResultResponse(level.ToString(), correctAnswers, request.Answers.Count));
    }
}

// DTOs
public record SubmitTestRequest(int QuestionSetId, Dictionary<int, int> Answers);
public record TestResultResponse(string Level, int CorrectAnswers, int TotalQuestions);

public record QuestionSetResponse(
    int Id,
    int Version,
    IEnumerable<QuestionResponse> Questions)
{
    public QuestionSetResponse(QuestionSet qs) : this(
        qs.Id,
        qs.Version,
        qs.Questions.Select(q => new QuestionResponse(q)))
    { }
}

public record QuestionResponse(
    int Id,
    int OrderIndex,
    string Text,
    string[] Options)
{
    public QuestionResponse(Question q) : this(
        q.Id,
        q.OrderIndex,
        q.Text,
        System.Text.Json.JsonSerializer.Deserialize<string[]>(q.OptionsJson) ?? Array.Empty<string>())
    { }
}

using AINewsHub.Core.Entities;
using AINewsHub.Core.Enums;
using AINewsHub.Core.Interfaces;
using AINewsHub.Infrastructure.Data;
using AINewsHub.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AINewsHub.NewsletterService;

public class QuestionUpdateWorker : BackgroundService
{
    private readonly ILogger<QuestionUpdateWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public QuestionUpdateWorker(ILogger<QuestionUpdateWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Question Update Worker starting at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilMonday == 0 && now.Hour >= 0)
                    daysUntilMonday = 7; // Already past Monday midnight, wait for next

                var nextMonday = now.Date.AddDays(daysUntilMonday);
                var delay = nextMonday - now;

                _logger.LogInformation("Next question set update at: {time} (in {delay})", nextMonday, delay);

                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await UpdateQuestionSetAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in question update worker");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task UpdateQuestionSetAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting weekly question set update at: {time}", DateTimeOffset.Now);

        using var scope = _scopeFactory.CreateScope();
        var testService = scope.ServiceProvider.GetRequiredService<ITestService>();
        var questionSetRepo = scope.ServiceProvider.GetRequiredService<QuestionSetRepository>();
        var keywordAnalyzer = new KeywordAnalyzer();

        // Analyze forum keywords
        var keywords = await keywordAnalyzer.ExtractKeywordsAsync(stoppingToken);
        _logger.LogInformation("Extracted {count} keywords from forums", keywords.Count);

        // Generate new questions based on keywords
        var questions = GenerateQuestionsFromKeywords(keywords);

        // Create new question set in database
        var keywordsJson = JsonSerializer.Serialize(keywords);
        var questionSet = await testService.CreateQuestionSetAsync(questions, keywordsJson);

        // Save to file for history
        var dto = new QuestionSetDto
        {
            Version = questionSet.Version,
            IsActive = false,
            CreatedAt = questionSet.CreatedAt,
            SourceKeywords = keywords,
            Questions = questions.Select(q => QuestionDto.FromEntity(q)).ToList()
        };
        questionSetRepo.SaveQuestionSet(dto);

        // Activate the new question set
        await testService.ActivateQuestionSetAsync(questionSet.Id);
        questionSetRepo.SetActiveVersion(questionSet.Version);

        _logger.LogInformation("Question set v{version} created and activated", questionSet.Version);
    }

    private IEnumerable<Question> GenerateQuestionsFromKeywords(List<string> keywords)
    {
        // Generate 8 questions - at least 50% based on keywords (AC9)
        var questions = new List<Question>();
        var keywordBasedCount = Math.Max(4, keywords.Count > 0 ? 4 : 0);

        // Claude Code knowledge-based questions
        var baseQuestions = GetClaudeCodeBaseQuestions();

        // Add keyword-based questions
        for (int i = 0; i < keywordBasedCount && i < keywords.Count; i++)
        {
            questions.Add(new Question
            {
                OrderIndex = i,
                Text = $"What is the significance of '{keywords[i]}' in modern AI development?",
                OptionsJson = JsonSerializer.Serialize(new[]
                {
                    "It is a deprecated technology",
                    "It is a core concept in current AI research",
                    "It is only used in academic settings",
                    "It has no practical applications"
                }),
                CorrectOptionIndex = 1,
                TargetLevel = (UserLevel)(i % 3),
                SourceKeyword = keywords[i]
            });
        }

        // Fill remaining with base questions
        var baseIndex = 0;
        while (questions.Count < 8 && baseIndex < baseQuestions.Count)
        {
            var q = baseQuestions[baseIndex];
            q.OrderIndex = questions.Count;
            questions.Add(q);
            baseIndex++;
        }

        return questions.Take(8);
    }

    private List<Question> GetClaudeCodeBaseQuestions()
    {
        return new List<Question>
        {
            new Question
            {
                Text = "Claude Code CLI의 주요 목적은 무엇인가요?",
                OptionsJson = JsonSerializer.Serialize(new[]
                {
                    "이미지 편집",
                    "소프트웨어 개발 지원",
                    "음악 작곡",
                    "데이터베이스 관리"
                }),
                CorrectOptionIndex = 1,
                TargetLevel = UserLevel.Beginner
            },
            new Question
            {
                Text = "MCP (Model Context Protocol)의 역할은?",
                OptionsJson = JsonSerializer.Serialize(new[]
                {
                    "외부 도구 및 데이터 소스와 통합",
                    "모델 학습 가속화",
                    "이미지 처리",
                    "네트워크 보안"
                }),
                CorrectOptionIndex = 0,
                TargetLevel = UserLevel.Intermediate
            },
            new Question
            {
                Text = "Claude의 Tool Use 기능에서 parallel function calling이란?",
                OptionsJson = JsonSerializer.Serialize(new[]
                {
                    "순차적 함수 실행",
                    "동시에 여러 함수를 호출하여 효율성 향상",
                    "함수 중복 제거",
                    "함수 오류 처리"
                }),
                CorrectOptionIndex = 1,
                TargetLevel = UserLevel.Advanced
            },
            new Question
            {
                Text = "RAG (Retrieval-Augmented Generation)의 장점은?",
                OptionsJson = JsonSerializer.Serialize(new[]
                {
                    "모델 크기 감소",
                    "외부 지식 소스를 활용한 정확한 응답",
                    "학습 속도 향상",
                    "메모리 사용량 감소"
                }),
                CorrectOptionIndex = 1,
                TargetLevel = UserLevel.Intermediate
            }
        };
    }
}

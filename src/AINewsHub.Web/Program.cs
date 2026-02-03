using AINewsHub.Core.Interfaces;
using AINewsHub.Infrastructure.Data;
using AINewsHub.Infrastructure.Services;
using AINewsHub.Infrastructure.Repositories;
using AINewsHub.Web.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (Aspire)
builder.AddServiceDefaults();

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add controllers for API endpoints
builder.Services.AddControllers();

// Configure SQLite with WAL mode
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=../../../data/ainewshub.db",
        sqlite => sqlite.CommandTimeout(30)));

// Register services
builder.Services.AddSingleton<ISnowflakeIdGenerator>(new SnowflakeIdGenerator(machineId: 1));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITagPreferenceService, TagPreferenceService>();
builder.Services.AddScoped<ITestService, TestService>();
builder.Services.AddScoped<IRssService, RssService>();
builder.Services.AddScoped<IArticleService, ArticleService>();

// Register repositories
builder.Services.AddScoped<TagXmlRepository>();

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Map controllers (API)
app.MapControllers();

// Map Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map Aspire default endpoints
app.MapDefaultEndpoints();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();

    // Seed initial data if needed
    await SeedDataAsync(context);
}

app.Run();

static async Task SeedDataAsync(AppDbContext context)
{
    // Seed sources if not exist
    if (!context.Sources.Any())
    {
        context.Sources.AddRange(
            new AINewsHub.Core.Entities.Source { Name = "Anthropic Blog", Url = "https://www.anthropic.com/news", CrawlerType = "Playwright", CrawlOffsetMinutes = 0 },
            new AINewsHub.Core.Entities.Source { Name = "Anthropic Research", Url = "https://www.anthropic.com/research", CrawlerType = "Playwright", CrawlOffsetMinutes = 1 },
            new AINewsHub.Core.Entities.Source { Name = "OpenAI Blog", Url = "https://openai.com/blog", CrawlerType = "Playwright", CrawlOffsetMinutes = 2 },
            new AINewsHub.Core.Entities.Source { Name = "Google AI Blog", Url = "https://blog.google/technology/ai", CrawlerType = "Playwright", CrawlOffsetMinutes = 3 },
            new AINewsHub.Core.Entities.Source { Name = "DeepMind Blog", Url = "https://deepmind.google/blog", CrawlerType = "Playwright", CrawlOffsetMinutes = 4 },
            new AINewsHub.Core.Entities.Source { Name = "Microsoft AI Blog", Url = "https://blogs.microsoft.com/ai", CrawlerType = "Playwright", CrawlOffsetMinutes = 5 },
            new AINewsHub.Core.Entities.Source { Name = "r/MachineLearning", Url = "https://reddit.com/r/MachineLearning", CrawlerType = "RedditApi", CrawlOffsetMinutes = 6 },
            new AINewsHub.Core.Entities.Source { Name = "r/LocalLLaMA", Url = "https://reddit.com/r/LocalLLaMA", CrawlerType = "RedditApi", CrawlOffsetMinutes = 7 },
            new AINewsHub.Core.Entities.Source { Name = "Hacker News", Url = "https://news.ycombinator.com", CrawlerType = "HackerNewsApi", CrawlOffsetMinutes = 8 }
        );
        await context.SaveChangesAsync();
    }

    // Seed tags if not exist
    if (!context.Tags.Any())
    {
        var tags = new[]
        {
            // Core AI concepts
            "LLM", "GPT", "Claude", "Gemini", "Llama", "Mistral",
            // Techniques
            "RAG", "Fine-tuning", "RLHF", "DPO", "Prompt Engineering",
            "Chain of Thought", "Few-shot Learning", "Zero-shot Learning",
            // Architecture
            "Transformer", "Attention", "Embedding", "Tokenization",
            "Multimodal", "Vision", "Audio", "Text-to-Image",
            // Tools & Platforms
            "MCP", "Tool Use", "Function Calling", "Agents", "Agentic AI",
            "Claude Code", "GitHub Copilot", "Cursor",
            // Safety & Ethics
            "AI Safety", "Alignment", "Constitutional AI", "RLAIF",
            "Hallucination", "Bias", "Red Teaming",
            // Infrastructure
            "Inference", "Quantization", "Distillation", "Deployment",
            "API", "SDK", "Cloud", "Edge AI",
            // Research
            "Benchmark", "Evaluation", "Scaling Laws", "Emergent Abilities",
            "Reasoning", "Math", "Coding", "Summarization"
        };

        foreach (var tagName in tags)
        {
            context.Tags.Add(new AINewsHub.Core.Entities.Tag { Name = tagName });
        }
        await context.SaveChangesAsync();
    }

    // Seed initial question set if not exist
    if (!context.QuestionSets.Any())
    {
        var questionSet = new AINewsHub.Core.Entities.QuestionSet
        {
            Version = 1,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow
        };
        context.QuestionSets.Add(questionSet);
        await context.SaveChangesAsync();

        var questions = new[]
        {
            new AINewsHub.Core.Entities.Question
            {
                QuestionSetId = questionSet.Id,
                OrderIndex = 0,
                Text = "Claude Code CLI의 주요 목적은 무엇인가요?",
                OptionsJson = "[\"이미지 편집\", \"소프트웨어 개발 지원\", \"음악 작곡\", \"데이터베이스 관리\"]",
                CorrectOptionIndex = 1,
                TargetLevel = AINewsHub.Core.Enums.UserLevel.Beginner
            },
            new AINewsHub.Core.Entities.Question
            {
                QuestionSetId = questionSet.Id,
                OrderIndex = 1,
                Text = "MCP (Model Context Protocol)의 역할은?",
                OptionsJson = "[\"외부 도구 및 데이터 소스와 통합\", \"모델 학습 가속화\", \"이미지 처리\", \"네트워크 보안\"]",
                CorrectOptionIndex = 0,
                TargetLevel = AINewsHub.Core.Enums.UserLevel.Intermediate
            },
            new AINewsHub.Core.Entities.Question
            {
                QuestionSetId = questionSet.Id,
                OrderIndex = 2,
                Text = "RAG (Retrieval-Augmented Generation)의 주요 장점은?",
                OptionsJson = "[\"모델 크기 감소\", \"외부 지식 소스를 활용한 정확한 응답\", \"학습 속도 향상\", \"메모리 사용량 감소\"]",
                CorrectOptionIndex = 1,
                TargetLevel = AINewsHub.Core.Enums.UserLevel.Intermediate
            },
            new AINewsHub.Core.Entities.Question
            {
                QuestionSetId = questionSet.Id,
                OrderIndex = 3,
                Text = "Claude의 Tool Use 기능은 무엇을 가능하게 하나요?",
                OptionsJson = "[\"이미지 생성\", \"외부 함수 및 API 호출\", \"자동 번역\", \"음성 인식\"]",
                CorrectOptionIndex = 1,
                TargetLevel = AINewsHub.Core.Enums.UserLevel.Beginner
            },
            new AINewsHub.Core.Entities.Question
            {
                QuestionSetId = questionSet.Id,
                OrderIndex = 4,
                Text = "Prompt Engineering에서 'few-shot learning'이란?",
                OptionsJson = "[\"모델을 처음부터 학습\", \"예시를 포함하여 모델의 응답 가이드\", \"적은 데이터로 전체 학습\", \"빠른 추론\"]",
                CorrectOptionIndex = 1,
                TargetLevel = AINewsHub.Core.Enums.UserLevel.Intermediate
            },
            new AINewsHub.Core.Entities.Question
            {
                QuestionSetId = questionSet.Id,
                OrderIndex = 5,
                Text = "RLHF (Reinforcement Learning from Human Feedback)의 목적은?",
                OptionsJson = "[\"모델 속도 향상\", \"인간 선호도에 맞게 모델 조정\", \"데이터 압축\", \"메모리 최적화\"]",
                CorrectOptionIndex = 1,
                TargetLevel = AINewsHub.Core.Enums.UserLevel.Advanced
            },
            new AINewsHub.Core.Entities.Question
            {
                QuestionSetId = questionSet.Id,
                OrderIndex = 6,
                Text = "Constitutional AI의 핵심 개념은?",
                OptionsJson = "[\"법률 문서 생성\", \"원칙 기반 AI 행동 제어\", \"헌법 분석\", \"정치 분류\"]",
                CorrectOptionIndex = 1,
                TargetLevel = AINewsHub.Core.Enums.UserLevel.Advanced
            },
            new AINewsHub.Core.Entities.Question
            {
                QuestionSetId = questionSet.Id,
                OrderIndex = 7,
                Text = "LLM에서 'hallucination'이란?",
                OptionsJson = "[\"환각 유발 기능\", \"사실이 아닌 정보를 자신있게 생성하는 현상\", \"비주얼 효과\", \"메모리 오류\"]",
                CorrectOptionIndex = 1,
                TargetLevel = AINewsHub.Core.Enums.UserLevel.Beginner
            }
        };

        context.Questions.AddRange(questions);
        await context.SaveChangesAsync();
    }
}

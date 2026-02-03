using AINewsHub.NewsletterService;
using AINewsHub.Core.Interfaces;
using AINewsHub.Infrastructure.Data;
using AINewsHub.Infrastructure.Services;
using AINewsHub.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Add service defaults (Aspire)
builder.AddServiceDefaults();

// Configure SQLite with WAL mode
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=../../../data/ainewshub.db",
        sqlite => sqlite.CommandTimeout(30)));

// Register services
builder.Services.AddSingleton<ISnowflakeIdGenerator>(new SnowflakeIdGenerator(machineId: 2));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<ITestService, TestService>();
builder.Services.AddScoped<IRssService, RssService>();

// Register repositories
var questionsPath = Path.Combine(AppContext.BaseDirectory, "../../../data/questions");
builder.Services.AddSingleton(new QuestionSetRepository(questionsPath));

// Register workers
builder.Services.AddHostedService<NewsletterWorker>();
builder.Services.AddHostedService<QuestionUpdateWorker>();

// Configure as Windows Service / Linux Daemon
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AINewsHub.NewsletterService";
});
builder.Services.AddSystemd();

var host = builder.Build();
host.Run();

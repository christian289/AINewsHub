using AINewsHub.CrawlerService;
using AINewsHub.Core.Interfaces;
using AINewsHub.Infrastructure.Data;
using AINewsHub.Infrastructure.Services;
using AINewsHub.Infrastructure.Crawlers;
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
builder.Services.AddSingleton<ISnowflakeIdGenerator>(new SnowflakeIdGenerator(machineId: 1));
builder.Services.AddScoped<IArticleService, ArticleService>();

// Register crawlers
builder.Services.AddScoped<PlaywrightCrawler>();
builder.Services.AddScoped<RedditApiClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var clientId = config["Reddit:ClientId"] ?? "";
    var clientSecret = config["Reddit:ClientSecret"] ?? "";
    var userAgent = config["Reddit:UserAgent"] ?? "AINewsHub/1.0";
    return new RedditApiClient(clientId, clientSecret, userAgent);
});
builder.Services.AddScoped<HackerNewsApiClient>();

// Register worker
builder.Services.AddHostedService<CrawlerWorker>();

// Configure as Windows Service / Linux Daemon
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AINewsHub.CrawlerService";
});
builder.Services.AddSystemd();

var host = builder.Build();
host.Run();

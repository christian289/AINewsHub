# AINewsHub - AI Article Collection Server

## Project Overview

AI 기사 수집 및 맞춤형 뉴스레터 서비스. 회원가입 없이 Snowflake ID 기반 익명 사용자 식별.

## Tech Stack

- **.NET 10** - Core framework
- **Blazor** - Web UI (Server-side)
- **Playwright** - Web crawling
- **.NET Aspire** - Service orchestration
- **Entity Framework Core** - ORM
- **SQLite** (WAL mode) → PostgreSQL migration planned

## Architecture

```
AINewsHub/
├── src/
│   ├── AINewsHub.AppHost/          # Aspire orchestrator
│   ├── AINewsHub.ServiceDefaults/  # Shared Aspire config
│   ├── AINewsHub.Core/             # Domain models, interfaces
│   ├── AINewsHub.Infrastructure/   # EF Core, crawlers, services
│   ├── AINewsHub.Web/              # Blazor web app
│   ├── AINewsHub.CrawlerService/   # Windows Service / Linux Daemon
│   └── AINewsHub.NewsletterService/ # Windows Service / Linux Daemon
├── tests/
├── data/
│   ├── ainewshub.db
│   ├── tags.xml
│   └── questions/
└── docs/
    └── PLAN-v3.md                  # Full project plan
```

## Key Concepts

### Snowflake ID (User Identification)

- 64-bit unique ID: `timestamp(41) + machineId(10) + sequence(12)`
- Zero collision guaranteed
- RSS URL format: `/rss/{snowflakeId}` (e.g., `/rss/7194859789123456789`)

### Tag Preferences

- Must-include tags: max 5
- Exclude tags: max 10
- Re-test limit: 7 days between tests

### Level Test

- 8 questions based on Claude Code knowledge
- Levels: Beginner, Intermediate, Advanced (internal, not shown to user)
- Weekly refresh based on forum keywords

## Crawling Sources

| Source | Method |
|--------|--------|
| Anthropic, OpenAI, Google, Microsoft blogs | Playwright |
| Reddit (r/MachineLearning, r/LocalLLaMA) | OAuth API |
| Hacker News | API |

## Development Commands

```bash
# Run with Aspire
dotnet run --project src/AINewsHub.AppHost

# Run individual services
dotnet run --project src/AINewsHub.Web
dotnet run --project src/AINewsHub.CrawlerService
dotnet run --project src/AINewsHub.NewsletterService

# Database migrations
dotnet ef migrations add <MigrationName> --project src/AINewsHub.Infrastructure --startup-project src/AINewsHub.Web
dotnet ef database update --project src/AINewsHub.Infrastructure --startup-project src/AINewsHub.Web

# Run tests
dotnet test
```

## Code Conventions

### Naming

- Entities: PascalCase (e.g., `User`, `Article`, `UserTagPreference`)
- Interfaces: `I` prefix (e.g., `IUserService`, `ISnowflakeIdGenerator`)
- Services: `*Service` suffix (e.g., `UserService`, `RssService`)

### Entity Patterns

```csharp
public class Entity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Navigation properties use collection expressions
    public ICollection<Related> Relations { get; set; } = [];
}
```

### Service Registration

```csharp
// In Program.cs or extension method
services.AddSingleton<ISnowflakeIdGenerator>(new SnowflakeIdGenerator(machineId: 1));
services.AddScoped<IUserService, UserService>();
```

## Background Services

### CrawlerService

- Runs every 10 minutes
- Staggered crawling (different sources at different offsets)
- Windows: `sc create AINewsHubCrawler binPath="..."`
- Linux: systemd service

### NewsletterService

- Daily at 00:00: Generate 2 articles per user level
- Weekly on Monday 00:00: Refresh question set based on forum keywords

## Important Files

| File | Purpose |
|------|---------|
| `docs/PLAN-v3.md` | Full project plan with all requirements |
| `data/tags.xml` | Accumulated tags (atomic writes) |
| `data/questions/*.json` | Question sets (version history preserved) |

## API Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/user/init` | POST | Create/get Snowflake ID |
| `/api/user/{id}` | GET | User info + preferences |
| `/api/user/{id}/can-retest` | GET | Check 7-day limit |
| `/api/user/{id}/preferences` | PUT | Save tag preferences |
| `/api/test/{id}/submit` | POST | Submit test, get level |
| `/rss/{snowflakeId}` | GET | Personalized RSS feed |

## Testing Guidelines

- Unit tests in `AINewsHub.Core.Tests`
- Integration tests in `AINewsHub.Integration.Tests`
- Test Snowflake ID generator for uniqueness
- Test tag preference limits (5 must-include, 10 exclude)
- Test 7-day re-test restriction

## Acceptance Criteria Reference

See `docs/PLAN-v3.md` Section 6 for full list (AC1-AC14).

Key metrics:
- Crawl success rate: ≥95%/day
- Snowflake ID collision: 0%
- Tag preference limits enforced
- RSS update: 00:00 ±5min

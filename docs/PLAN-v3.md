# AINewsHub 프로젝트 계획서 (최종 v3.1)

## 문서 정보
- **버전**: v3.1 (Snowflake ID 적용)
- **작성일**: 2026-02-04
- **상태**: 계획 승인 대기

---

## 1. 요구사항 요약

### 1.1 핵심 요구사항

| 구분 | 내용 |
|------|------|
| **목적** | AI 기사 수집 및 맞춤형 뉴스레터 제공 |
| **기술 스택** | .NET 10, Blazor, Playwright, .NET Aspire |
| **DB** | SQLite (WAL 모드) → PostgreSQL 마이그레이션 예정 |
| **프로세스** | Aspire 오케스트레이션, Windows Service / Linux Daemon |
| **사용자 인증** | 회원가입 없음 - **Snowflake ID 기반** 익명 식별 |
| **크롤링** | 10분마다 실행, 출처별 시간 분산 |
| **뉴스레터** | RSS 피드, 매일 0시 2개 생성 (5분 정독) |
| **레벨테스트** | 8문항, 3단계 (비노출), 주간 갱신, Claude Code 기반 |
| **재테스트** | 7일 제한, 태그 선호도 설정 (관심 5개, 제외 10개) |

### 1.2 크롤링 소스 (기본값, 수동 추가 가능)

| 카테고리 | 소스 | 방식 |
|----------|------|------|
| Anthropic | anthropic.com/news, /research | Playwright |
| OpenAI | openai.com/blog, /news | Playwright |
| Google | blog.google/technology/ai, deepmind.google/blog | Playwright |
| Microsoft | blogs.microsoft.com/ai | Playwright |
| Reddit | r/MachineLearning, r/LocalLLaMA | OAuth API |
| Hacker News | /frontpage AI 키워드 필터 | API |

---

## 2. 회원가입 없는 사용자 식별 시스템

### 2.1 Snowflake ID란?

Snowflake ID는 Twitter에서 개발한 분산 고유 ID 생성 알고리즘입니다.

```
64-bit 구조:
┌─────────────────────────────────────────────────────────────────┐
│ 1bit │      41 bits       │   10 bits   │      12 bits         │
│ sign │    timestamp       │  machine ID │     sequence         │
│  (0) │ (ms since epoch)   │  (worker)   │  (같은 ms 내 순번)   │
└─────────────────────────────────────────────────────────────────┘

예: 7194859789123456789 (19자리 숫자)
```

**장점:**
- **100% 충돌 없음**: 시간 + 머신 ID + 시퀀스 조합
- **시간 정렬 가능**: ID 순서 = 생성 시간 순서
- **짧은 길이**: UUID(36자) vs Snowflake(19자리 숫자)
- **URL 친화적**: 숫자만 사용하여 인코딩 불필요

### 2.2 Snowflake ID 생성 및 저장

```
1. 최초 방문 시:
   - 서버에서 Snowflake ID 생성 (예: 7194859789123456789)
   - LocalStorage + HttpOnly Cookie에 저장

2. 재방문 시:
   - LocalStorage/Cookie에서 Snowflake ID 복원
   - 기존 설정 유지

3. ID 분실 시 (브라우저 초기화):
   - RSS URL 입력으로 복구
   - URL에서 Snowflake ID 파싱하여 복원
```

### 2.3 RSS URL 구조

```
https://ainewshub.local/rss/{snowflake_id}
예: https://ainewshub.local/rss/7194859789123456789
```

**URL 특징:**
- 숫자만 사용하여 간결함
- 복사/붙여넣기 용이
- QR 코드 생성 시 더 작은 크기

---

## 3. 태그 선호도 선택 (재테스트 시)

### 3.1 제한 사항

- **관심 태그**: 최대 5개 선택 가능
- **제외 태그**: 최대 10개 선택 가능
- **재테스트**: 마지막 테스트로부터 7일 경과 필요

### 3.2 선택 화면 구성

```
📌 관심 태그 선택 (최대 5개)
"이 주제는 꼭 포함해주세요"

☑ LLM   ☑ Claude   ☐ GPT   ☐ Gemini   ☑ RAG
☐ Agents   ☑ MCP   ☑ Safety   ☐ RLHF   ...

선택됨: 5/5

────────────────────────────────────────

🚫 제외 태그 선택 (최대 10개)
"이 주제는 관심 없어요"

☑ Fine-tuning   ☑ Vision   ☑ Audio   ...

선택됨: 3/10
```

---

## 4. 엔티티 모델

### 4.1 User.cs (수정)

```csharp
public class User
{
    public int Id { get; set; }

    /// <summary>
    /// Snowflake ID - 64-bit unique identifier
    /// 충돌 없는 분산 고유 ID
    /// </summary>
    public long SnowflakeId { get; set; }

    public UserLevel Level { get; set; } = UserLevel.Beginner;
    public DateTime? LastTestDate { get; set; }
    public int TestCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<UserTagPreference> TagPreferences { get; set; } = [];
    public ICollection<TestHistory> TestHistories { get; set; } = [];
}

public enum UserLevel
{
    Beginner = 0,
    Intermediate = 1,
    Advanced = 2
}
```

### 4.2 SnowflakeIdGenerator.cs (신규)

```csharp
/// <summary>
/// Twitter Snowflake ID 생성기
/// 64-bit: 1(sign) + 41(timestamp) + 10(machine) + 12(sequence)
/// </summary>
public class SnowflakeIdGenerator
{
    private const long Epoch = 1704067200000L; // 2024-01-01 00:00:00 UTC
    private const int MachineBits = 10;
    private const int SequenceBits = 12;

    private readonly long _machineId;
    private long _sequence = 0;
    private long _lastTimestamp = -1;
    private readonly object _lock = new();

    public SnowflakeIdGenerator(long machineId = 1)
    {
        if (machineId < 0 || machineId >= (1 << MachineBits))
            throw new ArgumentException($"Machine ID must be between 0 and {(1 << MachineBits) - 1}");
        _machineId = machineId;
    }

    public long NextId()
    {
        lock (_lock)
        {
            var timestamp = GetTimestamp();

            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & ((1 << SequenceBits) - 1);
                if (_sequence == 0)
                    timestamp = WaitNextMillis(_lastTimestamp);
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestamp = timestamp;

            return ((timestamp - Epoch) << (MachineBits + SequenceBits))
                 | (_machineId << SequenceBits)
                 | _sequence;
        }
    }

    private static long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static long WaitNextMillis(long lastTimestamp)
    {
        var timestamp = GetTimestamp();
        while (timestamp <= lastTimestamp)
            timestamp = GetTimestamp();
        return timestamp;
    }
}
```

### 4.3 UserTagPreference.cs (신규)

```csharp
public class UserTagPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TagId { get; set; }
    public TagPreferenceType PreferenceType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

public enum TagPreferenceType
{
    MustInclude = 1,  // 최대 5개
    Exclude = 2       // 최대 10개
}
```

### 4.4 TestHistory.cs (신규)

```csharp
public class TestHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime TestDate { get; set; }
    public UserLevel ResultLevel { get; set; }
    public int QuestionSetId { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; } = 8;

    // Navigation
    public User User { get; set; } = null!;
    public QuestionSet QuestionSet { get; set; } = null!;
}
```

---

## 5. API 엔드포인트

| 엔드포인트 | 메서드 | 용도 |
|------------|--------|------|
| `/api/user/init` | POST | Snowflake ID 생성 또는 기존 반환 |
| `/api/user/{snowflakeId}` | GET | 사용자 정보 + 선호도 조회 |
| `/api/user/{snowflakeId}/can-retest` | GET | 재테스트 가능 여부 (7일 체크) |
| `/api/user/recover` | POST | RSS URL로 Snowflake ID 복구 |
| `/api/tags` | GET | 전체 태그 목록 (선호도 선택용) |
| `/api/user/{snowflakeId}/preferences` | PUT | 태그 선호도 저장 |
| `/api/test/questions` | GET | 현재 활성 문항 셋 |
| `/api/test/{snowflakeId}/submit` | POST | 테스트 제출 + 레벨 산정 |
| `/rss/{snowflakeId}` | GET | 개인화 RSS 피드 |

---

## 6. 수용 기준 (Acceptance Criteria)

| ID | 기준 | 측정 방법 |
|----|------|----------|
| AC1 | 크롤링 성공률 ≥95%/일 | 로그 분석 |
| AC2 | 중복 기사 <1% | URL+제목 퍼지 매칭 |
| AC3 | RSS 업데이트 0시 ±5분 | 타임스탬프 검증 |
| AC4 | 레벨테스트 완료율 ≥80% | DB 통계 |
| AC5 | 태그 커버리지 100% (1-10개/기사) | DB 쿼리 |
| AC6 | 본문 추출 품질 ≥90% (>100단어) | 자동 검증 |
| AC7 | 문항 셋 주간 갱신 성공 | 월요일 0시 후 새 버전 |
| AC8 | 문항 셋 이력 100% 보존 | questions/ 폴더 검증 |
| AC9 | 키워드 기반 문항 ≥50% | 출처 키워드 존재 |
| AC10 | Aspire Dashboard 정상 작동 | 3개 서비스 표시 |
| **AC11** | **Snowflake ID 충돌률 0%** | **DB unique 제약 검증** |
| **AC12** | **재테스트 7일 제한 준수** | **로직 테스트** |
| **AC13** | **태그 선호도 제한 (5/10) 준수** | **API 검증** |
| **AC14** | **ID 복구 성공률 ≥99%** | **E2E 테스트** |

---

## 7. 폴더 구조

```
AINewsHub/
├── src/
│   ├── AINewsHub.AppHost/
│   │   └── Program.cs
│   │
│   ├── AINewsHub.ServiceDefaults/
│   │   └── Extensions.cs
│   │
│   ├── AINewsHub.Core/
│   │   ├── Entities/
│   │   │   ├── Article.cs
│   │   │   ├── Tag.cs
│   │   │   ├── Source.cs
│   │   │   ├── User.cs                    # 수정 (SnowflakeId)
│   │   │   ├── UserTagPreference.cs       # 신규
│   │   │   ├── TestHistory.cs             # 신규
│   │   │   ├── QuestionSet.cs
│   │   │   └── Question.cs
│   │   ├── Enums/
│   │   │   ├── UserLevel.cs               # 신규
│   │   │   └── TagPreferenceType.cs       # 신규
│   │   └── Interfaces/
│   │       ├── IUserService.cs            # 신규
│   │       ├── ITagPreferenceService.cs   # 신규
│   │       ├── ISnowflakeIdGenerator.cs   # 신규
│   │       ├── ITestService.cs
│   │       └── IRssService.cs             # 신규
│   │
│   ├── AINewsHub.Infrastructure/
│   │   ├── Data/
│   │   │   └── AppDbContext.cs            # 수정 (DbSet 추가)
│   │   ├── Crawlers/
│   │   │   ├── PlaywrightCrawler.cs
│   │   │   ├── RedditApiClient.cs
│   │   │   └── HackerNewsApiClient.cs
│   │   ├── Repositories/
│   │   │   ├── TagXmlRepository.cs
│   │   │   └── QuestionSetRepository.cs
│   │   └── Services/
│   │       ├── SnowflakeIdGenerator.cs    # 신규
│   │       ├── UserService.cs             # 신규
│   │       ├── TagPreferenceService.cs    # 신규
│   │       ├── TestService.cs
│   │       └── RssService.cs              # 신규
│   │
│   ├── AINewsHub.Web/
│   │   ├── Components/
│   │   │   ├── UserInitializer.razor      # 신규 - Snowflake ID 관리
│   │   │   ├── TagPreferenceSelector.razor # 신규 - 태그 선택
│   │   │   └── RssRecovery.razor          # 신규 - ID 복구
│   │   ├── Pages/
│   │   │   ├── Index.razor
│   │   │   ├── LevelTest.razor
│   │   │   ├── TestResult.razor
│   │   │   ├── RssSubscribe.razor
│   │   │   └── Admin/
│   │   │       ├── Sources.razor
│   │   │       ├── QuestionSets.razor
│   │   │       └── Dashboard.razor
│   │   ├── Controllers/
│   │   │   ├── UserController.cs          # 신규
│   │   │   └── RssController.cs           # 신규
│   │   └── Program.cs
│   │
│   ├── AINewsHub.CrawlerService/
│   │   ├── Program.cs
│   │   ├── CrawlerWorker.cs
│   │   └── appsettings.json
│   │
│   └── AINewsHub.NewsletterService/
│       ├── Program.cs
│       ├── NewsletterWorker.cs
│       ├── QuestionUpdateWorker.cs
│       └── KeywordAnalyzer.cs
│
├── tests/
│   ├── AINewsHub.Core.Tests/
│   └── AINewsHub.Integration.Tests/
│
├── data/
│   ├── ainewshub.db
│   ├── tags.xml
│   └── questions/
│       ├── questionset_v001.json
│       └── ...
│
├── docs/
│   └── PLAN-v3.md                         # 이 문서
│
└── AINewsHub.sln
```

---

## 8. 구현 단계

### Phase 1: 프로젝트 기반 구축
- .NET Aspire 솔루션 생성
- Core/Infrastructure 프로젝트 생성
- EF Core + SQLite 설정 (WAL 모드)
- **Snowflake ID Generator 구현**
- 엔티티 모델 정의 (User with SnowflakeId, UserTagPreference, TestHistory)
- DB 마이그레이션

### Phase 2: 사용자 서비스
- IUserService 인터페이스 정의
- UserService 구현 (**Snowflake ID 생성/조회**)
- 7일 재테스트 제한 로직
- **Snowflake ID 복구 로직** (RSS URL 파싱)

### Phase 3: 태그 선호도 서비스
- ITagPreferenceService 인터페이스 정의
- TagPreferenceService 구현
- 관심 태그 5개 제한 로직
- 제외 태그 10개 제한 로직

### Phase 4: 크롤링 서비스
- Worker Service 프로젝트 생성
- Windows Service / Linux Daemon 설정
- Playwright 브라우저 풀 구현
- 각 소스별 크롤러 구현

### Phase 5: 태깅 시스템
- 태그 분류 체계 정의 (50-100개)
- 자동 태깅 로직 구현
- XML 파일 원자적 쓰기

### Phase 6: 레벨 테스트 + 뉴스레터 서비스
- 초기 문항 셋 생성 (Claude Code 기반)
- 레벨 테스트 엔진 구현
- 포럼 키워드 분석 서비스
- 개인화 RSS 피드 생성기

### Phase 7: 웹 UI
- Blazor 프로젝트 설정
- UserInitializer 컴포넌트 (**Snowflake ID 관리**)
- TagPreferenceSelector 컴포넌트
- RssRecovery 컴포넌트 (**ID 복구**)

### Phase 8: 안정화 및 모니터링
- Aspire Dashboard 활용
- 헬스체크 서비스 구현
- 통합 테스트 작성

---

## 9. 리스크 및 대응

| 리스크 | 영향 | 대응 방안 |
|--------|------|----------|
| 크롤링 차단 (403/429) | 높음 | 지수 백오프, User-Agent 로테이션, RSS 대체 |
| 사이트 구조 변경 | 중간 | Selector 버전 관리, 추출 실패율 알림 |
| SQLite 동시 쓰기 충돌 | 중간 | WAL 모드, 단일 Writer 큐 |
| 10분 주기 초과 | 중간 | 소스별 시간 분산, 회로 차단기 |
| XML 파일 손상 | 낮음 | 원자적 쓰기, 일일 백업 |
| Reddit ToS 위반 | 높음 | OAuth API 필수 사용 |
| ~~UUID 충돌~~ | ~~낮음~~ | ✅ **Snowflake ID로 해결 (충돌 불가)** |
| Snowflake ID 분실 | 중간 | RSS URL 복구 기능, 안내 강화 |
| 쿠키 차단 | 중간 | LocalStorage 우선, 복구 기능 |

---

## 10. UUID vs Snowflake ID 비교

| 항목 | UUID | Snowflake ID |
|------|------|--------------|
| **형식** | 550e8400-e29b-41d4-a716-446655440000 | 7194859789123456789 |
| **길이** | 36자 (하이픈 포함) | 19자리 숫자 |
| **충돌 가능성** | 이론적으로 존재 (2^122) | **0% (시간+머신+시퀀스)** |
| **정렬 가능** | 불가 | **가능 (시간순)** |
| **URL 친화성** | 인코딩 필요할 수 있음 | **숫자만 사용** |
| **저장 크기** | 16 bytes (binary) | **8 bytes (long)** |
| **생성 속도** | 빠름 | **매우 빠름** |

---

## 11. 결론

이 계획서는 AINewsHub 프로젝트의 모든 요구사항을 포함합니다:

1. ✅ 회원가입 없는 **Snowflake ID 기반** 사용자 식별 (충돌 0%)
2. ✅ 7일 재테스트 제한
3. ✅ 태그 선호도 선택 (관심 5개, 제외 10개)
4. ✅ 개인화 RSS 피드
5. ✅ .NET Aspire 오케스트레이션
6. ✅ Windows Service / Linux Daemon
7. ✅ 주간 문항 갱신
8. ✅ 문항 셋 이력 보존

**계획 승인 대기 중**

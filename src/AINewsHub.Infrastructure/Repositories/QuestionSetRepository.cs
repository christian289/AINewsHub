using System.Text.Json;
using System.Text.Json.Serialization;
using AINewsHub.Core.Entities;
using AINewsHub.Core.Enums;

namespace AINewsHub.Infrastructure.Repositories;

/// <summary>
/// Repository for managing question sets in JSON file format
/// Preserves version history by keeping all past versions
/// </summary>
public class QuestionSetRepository
{
    private readonly string _directoryPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public QuestionSetRepository(string directoryPath)
    {
        _directoryPath = directoryPath;

        if (!Directory.Exists(_directoryPath))
        {
            Directory.CreateDirectory(_directoryPath);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public IEnumerable<QuestionSetDto> GetAllQuestionSets()
    {
        var files = Directory.GetFiles(_directoryPath, "questions-v*.json")
            .OrderByDescending(f => f);

        foreach (var file in files)
        {
            var dto = LoadQuestionSet(file);
            if (dto != null)
                yield return dto;
        }
    }

    public QuestionSetDto? GetActiveQuestionSet()
    {
        var metadataPath = Path.Combine(_directoryPath, "active.json");
        if (!File.Exists(metadataPath))
            return GetLatestQuestionSet();

        var metadata = JsonSerializer.Deserialize<ActiveMetadata>(
            File.ReadAllText(metadataPath), _jsonOptions);

        if (metadata == null || metadata.ActiveVersion <= 0)
            return GetLatestQuestionSet();

        return GetQuestionSetByVersion(metadata.ActiveVersion);
    }

    public QuestionSetDto? GetLatestQuestionSet()
    {
        return GetAllQuestionSets().FirstOrDefault();
    }

    public QuestionSetDto? GetQuestionSetByVersion(int version)
    {
        var filePath = GetVersionFilePath(version);
        return File.Exists(filePath) ? LoadQuestionSet(filePath) : null;
    }

    public QuestionSetDto SaveQuestionSet(QuestionSetDto questionSet)
    {
        // Determine version
        if (questionSet.Version <= 0)
        {
            var maxVersion = GetAllQuestionSets()
                .Select(q => q.Version)
                .DefaultIfEmpty(0)
                .Max();
            questionSet.Version = maxVersion + 1;
        }

        questionSet.CreatedAt = DateTime.UtcNow;

        var filePath = GetVersionFilePath(questionSet.Version);
        var json = JsonSerializer.Serialize(questionSet, _jsonOptions);

        // Atomic write
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, filePath, overwrite: true);

        return questionSet;
    }

    public void SetActiveVersion(int version)
    {
        var metadataPath = Path.Combine(_directoryPath, "active.json");
        var metadata = new ActiveMetadata
        {
            ActiveVersion = version,
            ActivatedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(metadata, _jsonOptions);

        // Atomic write
        var tempPath = metadataPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, metadataPath, overwrite: true);
    }

    public QuestionSetDto CreateFromKeywords(IEnumerable<string> keywords, IEnumerable<QuestionDto> questions)
    {
        var dto = new QuestionSetDto
        {
            SourceKeywords = keywords.ToList(),
            Questions = questions.ToList(),
            IsActive = false
        };

        return SaveQuestionSet(dto);
    }

    private string GetVersionFilePath(int version) =>
        Path.Combine(_directoryPath, $"questions-v{version:D4}.json");

    private QuestionSetDto? LoadQuestionSet(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<QuestionSetDto>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private class ActiveMetadata
    {
        public int ActiveVersion { get; set; }
        public DateTime ActivatedAt { get; set; }
    }
}

/// <summary>
/// DTO for question set JSON serialization
/// </summary>
public class QuestionSetDto
{
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public List<string> SourceKeywords { get; set; } = [];
    public List<QuestionDto> Questions { get; set; } = [];

    public QuestionSet ToEntity()
    {
        return new QuestionSet
        {
            Version = Version,
            IsActive = IsActive,
            CreatedAt = CreatedAt,
            ActivatedAt = ActivatedAt,
            SourceKeywords = JsonSerializer.Serialize(SourceKeywords),
            Questions = Questions.Select((q, i) => q.ToEntity(i)).ToList()
        };
    }
}

/// <summary>
/// DTO for question JSON serialization
/// </summary>
public class QuestionDto
{
    public string Text { get; set; } = string.Empty;
    public List<string> Options { get; set; } = [];
    public int CorrectOptionIndex { get; set; }
    public UserLevel TargetLevel { get; set; }
    public string? SourceKeyword { get; set; }

    public Question ToEntity(int orderIndex)
    {
        return new Question
        {
            OrderIndex = orderIndex,
            Text = Text,
            OptionsJson = JsonSerializer.Serialize(Options),
            CorrectOptionIndex = CorrectOptionIndex,
            TargetLevel = TargetLevel,
            SourceKeyword = SourceKeyword
        };
    }

    public static QuestionDto FromEntity(Question entity)
    {
        return new QuestionDto
        {
            Text = entity.Text,
            Options = JsonSerializer.Deserialize<List<string>>(entity.OptionsJson) ?? [],
            CorrectOptionIndex = entity.CorrectOptionIndex,
            TargetLevel = entity.TargetLevel,
            SourceKeyword = entity.SourceKeyword
        };
    }
}

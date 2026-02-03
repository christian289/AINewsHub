using System.Text.RegularExpressions;

namespace AINewsHub.NewsletterService;

public class KeywordAnalyzer
{
    private readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did", "will", "would", "could", "should",
        "may", "might", "must", "shall", "can", "need", "dare", "ought", "used",
        "to", "of", "in", "for", "on", "with", "at", "by", "from", "up", "about",
        "into", "over", "after", "beneath", "under", "above", "this", "that", "these",
        "those", "i", "you", "he", "she", "it", "we", "they", "what", "which", "who",
        "when", "where", "why", "how", "all", "each", "every", "both", "few", "more",
        "most", "other", "some", "such", "no", "nor", "not", "only", "own", "same",
        "so", "than", "too", "very", "just", "and", "but", "if", "or", "because",
        "as", "until", "while", "although", "though", "since", "unless"
    };

    // AI-related keywords to prioritize
    private readonly HashSet<string> _aiKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "LLM", "GPT", "Claude", "Gemini", "transformer", "attention", "embedding",
        "fine-tuning", "RLHF", "RAG", "agent", "MCP", "prompt", "context", "token",
        "multimodal", "vision", "audio", "safety", "alignment", "hallucination",
        "inference", "latency", "throughput", "quantization", "distillation",
        "benchmark", "evaluation", "API", "tool-use", "function-calling"
    };

    public async Task<List<string>> ExtractKeywordsAsync(CancellationToken stoppingToken = default)
    {
        // In production, this would fetch from Reddit/HN and analyze
        // For now, return common AI keywords that can be used for questions
        var keywords = new List<string>
        {
            "RAG", "MCP", "Tool Use", "Prompt Engineering", "Context Window",
            "Fine-tuning", "RLHF", "Multi-agent", "Agentic AI", "Claude Code"
        };

        await Task.Delay(100, stoppingToken); // Simulate async operation

        return keywords;
    }

    public List<string> ExtractKeywordsFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // Tokenize and clean
        var words = Regex.Split(text.ToLower(), @"\W+")
            .Where(w => w.Length > 2)
            .Where(w => !_stopWords.Contains(w))
            .ToList();

        // Count frequencies
        var frequencies = words
            .GroupBy(w => w)
            .ToDictionary(g => g.Key, g => g.Count());

        // Prioritize AI keywords
        var result = frequencies
            .OrderByDescending(kv => _aiKeywords.Contains(kv.Key) ? kv.Value * 10 : kv.Value)
            .Take(20)
            .Select(kv => kv.Key)
            .ToList();

        return result;
    }
}

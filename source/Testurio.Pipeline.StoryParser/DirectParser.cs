using System.Text.RegularExpressions;
using Testurio.Core.Models;

namespace Testurio.Pipeline.StoryParser;

/// <summary>
/// Parses a template-conformant <see cref="WorkItem"/> directly into a <see cref="ParsedStory"/>
/// using rule-based heuristics. No Claude API call is made on this path.
/// </summary>
public sealed class DirectParser
{
    // Heuristic keyword sets for entity, action, and edge-case detection.
    private static readonly string[] EntityKeywords =
        ["user", "account", "order", "product", "cart", "item", "customer", "admin", "project", "report", "invoice", "payment", "subscription", "token", "session", "record", "entry", "file", "document"];

    private static readonly string[] ActionKeywords =
        ["create", "update", "delete", "submit", "login", "logout", "register", "cancel", "approve", "reject", "send", "receive", "upload", "download", "search", "filter", "export", "import", "view", "edit", "add", "remove", "assign", "complete", "start", "stop", "enable", "disable"];

    private static readonly string[] EdgeCaseKeywords =
        ["invalid", "empty", "null", "missing", "duplicate", "expired", "unauthorized", "forbidden", "not found", "error", "fail", "exceed", "limit", "concurrent", "timeout", "overflow", "boundary", "edge case", "negative", "zero", "max", "min"];

    /// <summary>
    /// Converts the <paramref name="workItem"/> into a <see cref="ParsedStory"/> using heuristic extraction.
    /// Entities, actions, and edge cases are extracted from the combined description and acceptance criteria text;
    /// if none are detected the corresponding arrays are empty (never null).
    /// </summary>
    public ParsedStory Parse(WorkItem workItem)
    {
        var acceptanceCriteria = SplitAcceptanceCriteria(workItem.AcceptanceCriteria);
        var combinedText = $"{workItem.Description} {workItem.AcceptanceCriteria}";

        return new ParsedStory
        {
            Title = workItem.Title.Trim(),
            Description = workItem.Description.Trim(),
            AcceptanceCriteria = acceptanceCriteria,
            Entities = ExtractKeywordMatches(combinedText, EntityKeywords),
            Actions = ExtractKeywordMatches(combinedText, ActionKeywords),
            EdgeCases = ExtractKeywordMatches(combinedText, EdgeCaseKeywords)
        };
    }

    /// <summary>
    /// Splits the raw acceptance criteria text into individual criterion strings.
    /// Handles numbered lists (1. …), bullet lists (- …, * …), and newline-delimited entries.
    /// </summary>
    private static IReadOnlyList<string> SplitAcceptanceCriteria(string raw)
    {
        // Split on numbered list markers, bullets, or newlines.
        var lines = Regex.Split(raw, @"(?m)^\s*(?:\d+\.|[-*])\s+|[\r\n]+")
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        // If splitting produced nothing (single-line, no markers), treat the whole text as one criterion.
        if (lines.Count == 0)
            lines.Add(raw.Trim());

        return lines.AsReadOnly();
    }

    /// <summary>
    /// Returns the subset of <paramref name="keywords"/> that appear (case-insensitive, whole-word)
    /// in <paramref name="text"/>, deduplicated and ordered by first occurrence.
    /// </summary>
    private static IReadOnlyList<string> ExtractKeywordMatches(string text, string[] keywords)
    {
        var lower = text.ToLowerInvariant();
        var found = new List<string>();
        foreach (var keyword in keywords)
        {
            // Use word-boundary matching for single-word keywords; substring for phrases.
            bool matched = keyword.Contains(' ')
                ? lower.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                : Regex.IsMatch(lower, $@"\b{Regex.Escape(keyword)}\b");

            if (matched && !found.Contains(keyword))
                found.Add(keyword);
        }
        return found.AsReadOnly();
    }
}

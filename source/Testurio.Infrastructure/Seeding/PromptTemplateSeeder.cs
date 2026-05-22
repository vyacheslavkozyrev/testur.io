using Microsoft.Azure.Cosmos;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.Seeding;

/// <summary>Abstraction for prompt template seeding; injectable in tests.</summary>
public interface IPromptTemplateSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Seeds the initial <see cref="PromptTemplate"/> documents into the <c>PromptTemplates</c>
/// Cosmos DB container at worker/API startup (feature 0028, extended in 0047).
/// Idempotent — skips documents that already exist so manual edits are preserved.
/// <para>
/// Feature 0047: extended to cover all five MVP pipeline stages using the unified schema
/// (Stage, Body, IsActive, Version, CreatedAt, UpdatedAt). The two pre-existing generator
/// documents are migrated to the new schema; three new documents are added for StoryParser,
/// AgentRouter, and ReportWriter.
/// </para>
/// </summary>
public sealed class PromptTemplateSeeder : IPromptTemplateSeeder
{
    // ─── Story Parser (Stage 1) ───────────────────────────────────────────────

    private static PromptTemplate StoryParserTemplate(DateTimeOffset now) => new()
    {
        Id = "story_parser",
        Stage = "story_parser",
        TemplateType = "story_parser",
        Version = 1,
        IsActive = true,
        CreatedAt = now,
        UpdatedAt = now,
        Body =
            """
            You are a story parsing assistant for an automated software testing platform.

            Given a raw work item (user story or bug report), extract and return ONLY a valid JSON object
            matching this exact schema (no markdown, no explanation, no code fences):

            {
              "title": "<string — non-empty>",
              "description": "<string — non-empty>",
              "acceptance_criteria": ["<string>", ...],
              "entities": ["<string>", ...],
              "actions": ["<string>", ...],
              "edge_cases": ["<string>", ...]
            }

            Rules:
            - title, description, and acceptance_criteria are REQUIRED and must be non-empty.
            - entities, actions, and edge_cases may be empty arrays [] when not applicable.
            - Do NOT include any text outside the JSON object.
            """
    };

    // ─── Agent Router (Stage 2) ───────────────────────────────────────────────

    private static PromptTemplate AgentRouterTemplate(DateTimeOffset now) => new()
    {
        Id = "agent_router",
        Stage = "agent_router",
        TemplateType = "agent_router",
        Version = 1,
        IsActive = true,
        CreatedAt = now,
        UpdatedAt = now,
        Body =
            """
            You are a test-type classification assistant for an automated software testing platform.

            Given a parsed user story, determine which of the following test types are meaningful to test
            the described functionality. Return ONLY a valid JSON object matching this exact schema
            (no markdown, no explanation, no code fences):

            {
              "test_types": ["api", "ui_e2e"],
              "reason": "<brief rationale — 1–3 sentences>"
            }

            Rules:
            - "test_types" must be an array containing zero or more of the values "api" and "ui_e2e".
            - Include "api" when the story describes backend behaviour, data operations, or HTTP endpoints.
            - Include "ui_e2e" when the story describes user-facing interactions, navigation, or visual feedback.
            - Include both when the story involves end-to-end flows that touch both API and UI.
            - Use an empty array [] when the story describes infrastructure, configuration, or non-testable concerns.
            - "reason" must be a non-empty string explaining the classification decision.
            - Do NOT include any text outside the JSON object.
            """
    };

    // ─── API Test Generator (Stage 4) ────────────────────────────────────────

    private static PromptTemplate ApiTestGeneratorTemplate(DateTimeOffset now) => new()
    {
        Id = "api_test_generator",
        Stage = "api_test_generator",
        TemplateType = "api_test_generator",
        Version = 1,
        IsActive = true,
        CreatedAt = now,
        UpdatedAt = now,
        Body =
            "You are an expert API test engineer. Your task is to produce a JSON array of " +
            "API test scenarios derived from the user story provided. Each scenario must be " +
            "precise, deterministic, and directly executable by an HTTP client without further " +
            "interpretation. Output only valid JSON — no markdown fences, no commentary.\n\n" +
            "Generate up to 10 API test scenarios for the story above. " +
            "Return a JSON array where each element has the following shape:\n" +
            "{\n" +
            "  \"id\": \"<UUID v4>\",\n" +
            "  \"title\": \"<short description>\",\n" +
            "  \"method\": \"GET|POST|PUT|PATCH|DELETE\",\n" +
            "  \"path\": \"<path and query only, no origin>\",\n" +
            "  \"headers\": { \"<name>\": \"<value>\" } | null,\n" +
            "  \"body\": { } | null,\n" +
            "  \"assertions\": [\n" +
            "    { \"type\": \"status_code\", \"expected\": <int> },\n" +
            "    { \"type\": \"json_path\", \"path\": \"<JSONPath>\", \"expected\": \"<value or *>\" },\n" +
            "    { \"type\": \"header\", \"name\": \"<header name>\", \"expected\": \"<value>\" }\n" +
            "  ]\n" +
            "}\n" +
            "Rules:\n" +
            "- Every scenario must include at least one status_code assertion.\n" +
            "- Do not exceed 10 scenarios.\n" +
            "- Output only the JSON array — no markdown, no explanation."
    };

    // ─── UI E2E Test Generator (Stage 4) ─────────────────────────────────────

    private static PromptTemplate UiE2eTestGeneratorTemplate(DateTimeOffset now) => new()
    {
        Id = "ui_e2e_test_generator",
        Stage = "ui_e2e_test_generator",
        TemplateType = "ui_e2e_test_generator",
        Version = 1,
        IsActive = true,
        CreatedAt = now,
        UpdatedAt = now,
        Body =
            "You are an expert UI end-to-end test engineer specialising in Playwright automation. " +
            "Your task is to produce a JSON array of UI test scenarios derived from the user story " +
            "provided. Each scenario must be a complete, ordered sequence of browser steps that a " +
            "Playwright script can execute without further interpretation. " +
            "Output only valid JSON — no markdown fences, no commentary.\n\n" +
            "Generate up to 5 UI end-to-end test scenarios for the story above. " +
            "Return a JSON array where each element has the following shape:\n" +
            "{\n" +
            "  \"id\": \"<UUID v4>\",\n" +
            "  \"title\": \"<short description>\",\n" +
            "  \"steps\": [\n" +
            "    { \"action\": \"navigate\", \"url\": \"<full URL>\" },\n" +
            "    { \"action\": \"click\", \"selector\": \"<locator>\" },\n" +
            "    { \"action\": \"fill\", \"selector\": \"<locator>\", \"value\": \"<text>\" },\n" +
            "    { \"action\": \"assert_visible\", \"selector\": \"<locator>\" },\n" +
            "    { \"action\": \"assert_text\", \"selector\": \"<locator>\", \"expected\": \"<text>\" },\n" +
            "    { \"action\": \"assert_url\", \"expected\": \"<url or prefix>\" }\n" +
            "  ]\n" +
            "}\n" +
            "Rules:\n" +
            "- Selector preference order: (1) Playwright role/text/label locators " +
            "(e.g. role=button[name=\"Submit\"]), (2) data-testid attributes " +
            "(e.g. [data-testid=\"submit-btn\"]), (3) CSS selectors as last resort.\n" +
            "- Every scenario must end with at least one assertion step " +
            "(assert_visible, assert_text, or assert_url).\n" +
            "- Do not exceed 5 scenarios.\n" +
            "- Output only the JSON array — no markdown, no explanation."
    };

    // ─── Report Writer (Stage 6) ──────────────────────────────────────────────

    private static PromptTemplate ReportWriterTemplate(DateTimeOffset now) => new()
    {
        Id = "report_writer",
        Stage = "report_writer",
        TemplateType = "report_writer",
        Version = 1,
        IsActive = true,
        CreatedAt = now,
        UpdatedAt = now,
        Body =
            """
            You are a QA analyst assistant. You will be given a test execution result and must produce a structured JSON report.
            Return ONLY valid JSON — no markdown fences, no commentary.
            The JSON must have exactly three top-level fields: "verdict", "recommendation", and "scenario_summaries".

            Rules:
            - "verdict": "PASSED" if every scenario passed, "FAILED" otherwise.
            - "recommendation": one of exactly "approve", "request_fixes", or "flag_for_manual_review".
              - "approve" when verdict is "PASSED" and no execution warnings.
              - "request_fixes" when verdict is "FAILED" and all failures have clear assertion diffs or step errors (no infrastructure exceptions).
              - "flag_for_manual_review" when verdict is "FAILED" and at least one failure is an infrastructure-level exception, or when execution warnings are present.
            - "scenario_summaries": array of objects, one per executed scenario, in execution order.
              Each object: { "scenario_id": string, "title": string, "passed": bool, "duration_ms": number, "error_summary": string|null }.
              For failed API scenarios: error_summary lists assertion diffs as "Expected: <v> / Actual: <v>".
              For failed UI E2E scenarios: error_summary lists the first step error message and step index.
              error_summary is null when passed is true.
            """
    };

    private readonly Container _container;

    public PromptTemplateSeeder(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "PromptTemplates");
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await SeedTemplateAsync(StoryParserTemplate(now), cancellationToken);
        await SeedTemplateAsync(AgentRouterTemplate(now), cancellationToken);
        await SeedTemplateAsync(ApiTestGeneratorTemplate(now), cancellationToken);
        await SeedTemplateAsync(UiE2eTestGeneratorTemplate(now), cancellationToken);
        await SeedTemplateAsync(ReportWriterTemplate(now), cancellationToken);
    }

    private async Task SeedTemplateAsync(PromptTemplate template, CancellationToken cancellationToken)
    {
        try
        {
            await _container.ReadItemAsync<PromptTemplate>(
                template.Id,
                new PartitionKey(template.TemplateType),
                cancellationToken: cancellationToken);
            // Document already exists — skip to preserve any manual edits.
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await _container.CreateItemAsync(
                template,
                new PartitionKey(template.TemplateType),
                cancellationToken: cancellationToken);
        }
    }
}

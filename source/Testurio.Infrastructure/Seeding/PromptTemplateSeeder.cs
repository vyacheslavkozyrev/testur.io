using Microsoft.Azure.Cosmos;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.Seeding;

/// <summary>
/// Seeds the initial <see cref="PromptTemplate"/> documents into the <c>PromptTemplates</c>
/// Cosmos DB container at worker startup (feature 0028).
/// <para>
/// Uses an <c>upsert</c> strategy so the seeder is idempotent: re-running it does not overwrite
/// documents that have been updated via the Cosmos portal or a manual migration.
/// However, to avoid silently downgrading manually edited templates, the seeder only upserts
/// when the document does not already exist (checked via a point-read before write).
/// </para>
/// Call <see cref="SeedAsync"/> once at host startup, before the worker begins processing messages.
/// </summary>
public sealed class PromptTemplateSeeder
{
    private const string PartitionKeyValue = "template";

    private static readonly PromptTemplate ApiTestGeneratorTemplate = new()
    {
        Id = "api_test_generator",
        TemplateType = "api_test_generator",
        Version = "1.0.0",
        SystemPrompt =
            "You are an expert API test engineer. Your task is to produce a JSON array of " +
            "API test scenarios derived from the user story provided. Each scenario must be " +
            "precise, deterministic, and directly executable by an HTTP client without further " +
            "interpretation. Output only valid JSON — no markdown fences, no commentary.",
        GeneratorInstruction =
            "Generate up to {{maxScenarios}} API test scenarios for the story above. " +
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
            "- Do not exceed {{maxScenarios}} scenarios.\n" +
            "- Output only the JSON array — no markdown, no explanation.",
        MaxScenarios = 10
    };

    private static readonly PromptTemplate UiE2eTestGeneratorTemplate = new()
    {
        Id = "ui_e2e_test_generator",
        TemplateType = "ui_e2e_test_generator",
        Version = "1.0.0",
        SystemPrompt =
            "You are an expert UI end-to-end test engineer specialising in Playwright automation. " +
            "Your task is to produce a JSON array of UI test scenarios derived from the user story " +
            "provided. Each scenario must be a complete, ordered sequence of browser steps that a " +
            "Playwright script can execute without further interpretation. " +
            "Output only valid JSON — no markdown fences, no commentary.",
        GeneratorInstruction =
            "Generate up to {{maxScenarios}} UI end-to-end test scenarios for the story above. " +
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
            "- Do not exceed {{maxScenarios}} scenarios.\n" +
            "- Output only the JSON array — no markdown, no explanation.",
        MaxScenarios = 5
    };

    private readonly Container _container;

    public PromptTemplateSeeder(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "PromptTemplates");
    }

    /// <summary>
    /// Upserts the initial seed templates into the <c>PromptTemplates</c> container.
    /// Existing documents are not overwritten — a point-read is performed first, and the
    /// upsert is skipped when the document already exists, preserving any manual edits.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedTemplateAsync(ApiTestGeneratorTemplate, cancellationToken);
        await SeedTemplateAsync(UiE2eTestGeneratorTemplate, cancellationToken);
    }

    private async Task SeedTemplateAsync(PromptTemplate template, CancellationToken cancellationToken)
    {
        try
        {
            // Point-read first — skip upsert when the document already exists.
            await _container.ReadItemAsync<PromptTemplate>(
                template.Id,
                new PartitionKey(PartitionKeyValue),
                cancellationToken: cancellationToken);

            // Document exists; skip seeding to preserve any manual edits.
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Document does not exist — create it with the seed data.
            // Store a wrapper with the partition key field included.
            var document = new PromptTemplateDocument(template, PartitionKeyValue);
            await _container.CreateItemAsync(
                document,
                new PartitionKey(PartitionKeyValue),
                cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Internal wrapper that adds the <c>pk</c> partition key field required by the Cosmos container.
    /// </summary>
    private sealed record PromptTemplateDocument(PromptTemplate Template, string Pk)
    {
        public string Id => Template.Id;
        public string TemplateType => Template.TemplateType;
        public string Version => Template.Version;
        public string SystemPrompt => Template.SystemPrompt;
        public string GeneratorInstruction => Template.GeneratorInstruction;
        public int MaxScenarios => Template.MaxScenarios;
    }
}

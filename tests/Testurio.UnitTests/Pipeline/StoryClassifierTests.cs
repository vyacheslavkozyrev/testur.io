using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.AgentRouter;

namespace Testurio.UnitTests.Pipeline;

public class StoryClassifierTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();

    private StoryClassifier CreateSut() =>
        new(_llmClient.Object, NullLogger<StoryClassifier>.Instance);

    private static ParsedStory MakeStory(string title = "Add item to cart") => new()
    {
        Title = title,
        Description = "User adds a product to the cart.",
        AcceptanceCriteria = new[] { "Cart total updates", "Item count increments" }
    };

    private void SetupLlmResponse(string json)
    {
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
    }

    // ─── api-only result ──────────────────────────────────────────────────────

    [Fact]
    public async Task ClassifyAsync_ApiOnlyResponse_ReturnsApiType()
    {
        SetupLlmResponse("""{ "test_types": ["api"], "reason": "Story describes an API endpoint." }""");

        var sut = CreateSut();
        var (types, reason) = await sut.ClassifyAsync(MakeStory());

        Assert.Single(types);
        Assert.Equal(TestType.Api, types[0]);
        Assert.Equal("Story describes an API endpoint.", reason);
    }

    // ─── ui_e2e-only result ───────────────────────────────────────────────────

    [Fact]
    public async Task ClassifyAsync_UiE2eOnlyResponse_ReturnsUiE2eType()
    {
        SetupLlmResponse("""{ "test_types": ["ui_e2e"], "reason": "Story describes a UI interaction." }""");

        var sut = CreateSut();
        var (types, reason) = await sut.ClassifyAsync(MakeStory());

        Assert.Single(types);
        Assert.Equal(TestType.UiE2e, types[0]);
        Assert.NotEmpty(reason);
    }

    // ─── both types ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ClassifyAsync_BothTypesResponse_ReturnsBothTypes()
    {
        SetupLlmResponse("""{ "test_types": ["api", "ui_e2e"], "reason": "Story covers both API and UI." }""");

        var sut = CreateSut();
        var (types, reason) = await sut.ClassifyAsync(MakeStory());

        Assert.Equal(2, types.Length);
        Assert.Contains(TestType.Api, types);
        Assert.Contains(TestType.UiE2e, types);
        Assert.NotEmpty(reason);
    }

    // ─── empty JSON array ─────────────────────────────────────────────────────

    [Fact]
    public async Task ClassifyAsync_EmptyTestTypesArray_ReturnsEmptyTypes()
    {
        SetupLlmResponse("""{ "test_types": [], "reason": "Story describes infrastructure config." }""");

        var sut = CreateSut();
        var (types, reason) = await sut.ClassifyAsync(MakeStory());

        Assert.Empty(types);
        Assert.NotEmpty(reason);
    }

    // ─── unknown type values are ignored ─────────────────────────────────────

    [Fact]
    public async Task ClassifyAsync_UnknownTypeInResponse_IgnoresUnknownAndReturnsKnown()
    {
        SetupLlmResponse("""{ "test_types": ["api", "smoke"], "reason": "Only api is valid MVP type." }""");

        var sut = CreateSut();
        var (types, reason) = await sut.ClassifyAsync(MakeStory());

        Assert.Single(types);
        Assert.Equal(TestType.Api, types[0]);
        Assert.NotEmpty(reason);
    }

    // ─── case-insensitive matching ────────────────────────────────────────────

    [Fact]
    public async Task ClassifyAsync_UpperCaseTypeValues_ParsesCorrectly()
    {
        SetupLlmResponse("""{ "test_types": ["API", "UI_E2E"], "reason": "Case insensitive." }""");

        var sut = CreateSut();
        var (types, reason) = await sut.ClassifyAsync(MakeStory());

        Assert.Equal(2, types.Length);
        Assert.Contains(TestType.Api, types);
        Assert.Contains(TestType.UiE2e, types);
    }

    // ─── Claude API error → exception propagated ─────────────────────────────

    [Fact]
    public async Task ClassifyAsync_ClaudeApiThrows_ExceptionPropagated()
    {
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Claude API unreachable"));

        var sut = CreateSut();

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.ClassifyAsync(MakeStory()));
    }

    // ─── malformed JSON response ──────────────────────────────────────────────

    [Fact]
    public async Task ClassifyAsync_MalformedJson_ThrowsInvalidOperationException()
    {
        SetupLlmResponse("this is not json");

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ClassifyAsync(MakeStory()));
        Assert.Contains("failed to parse", ex.Message);
    }

    // ─── missing reason field ─────────────────────────────────────────────────

    [Fact]
    public async Task ClassifyAsync_MissingReasonField_ThrowsInvalidOperationException()
    {
        SetupLlmResponse("""{ "test_types": ["api"] }""");

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ClassifyAsync(MakeStory()));
        Assert.Contains("reason", ex.Message);
    }

    // ─── code fence stripping ─────────────────────────────────────────────────

    [Fact]
    public async Task ClassifyAsync_ResponseWrappedInCodeFences_StillParses()
    {
        var json =
            """
            ```json
            { "test_types": ["api"], "reason": "Code fences should be stripped." }
            ```
            """;

        SetupLlmResponse(json);

        var sut = CreateSut();
        var (types, reason) = await sut.ClassifyAsync(MakeStory());

        Assert.Single(types);
        Assert.Equal(TestType.Api, types[0]);
    }
}

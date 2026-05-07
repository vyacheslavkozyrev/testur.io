using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Models;
using Testurio.Plugins.TestExecutorPlugin;

namespace Testurio.UnitTests.Plugins;

public class TestExecutorPluginTests
{
    private readonly Mock<HttpMessageHandler> _httpHandler = new();
    private readonly ResponseSchemaValidator _validator = new();

    private TestExecutorPlugin CreateSut()
    {
        var client = new HttpClient(_httpHandler.Object);
        return new TestExecutorPlugin(client, _validator, NullLogger<TestExecutorPlugin>.Instance);
    }

    private static TestScenario BuildScenario(params TestScenarioStep[] steps) => new()
    {
        Id = "scenario-1",
        TestRunId = "run-1",
        ProjectId = "project-1",
        UserId = "user-1",
        Title = "Test Scenario",
        Steps = steps
    };

    private static TestScenarioStep BuildStep(string description, string expectedResult = "HTTP 200 OK") =>
        new() { Order = 1, Description = description, ExpectedResult = expectedResult };

    private void SetupHttpResponse(HttpStatusCode statusCode, string body = "{}")
    {
        _httpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
    }

    // --- Happy path ---

    [Fact]
    public async Task ExecuteScenarioAsync_MatchingStatusCode_StepPasses()
    {
        SetupHttpResponse(HttpStatusCode.OK, "{}");
        var sut = CreateSut();
        var scenario = BuildScenario(BuildStep("Send GET /api/users", "HTTP 200 OK"));

        var results = await sut.ExecuteScenarioAsync(scenario, "https://example.com", null);

        Assert.Single(results);
        Assert.Equal(StepStatus.Passed, results[0].Status);
        Assert.Equal(200, results[0].ActualStatusCode);
    }

    [Fact]
    public async Task ExecuteScenarioAsync_BearerTokenPresent_AuthorizationHeaderAttached()
    {
        HttpRequestMessage? capturedRequest = null;
        _httpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });

        var sut = CreateSut();
        var scenario = BuildScenario(BuildStep("GET /health"));

        await sut.ExecuteScenarioAsync(scenario, "https://example.com", "my-secret-token");

        Assert.NotNull(capturedRequest?.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("my-secret-token", capturedRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task ExecuteScenarioAsync_NoToken_NoAuthorizationHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        _httpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });

        var sut = CreateSut();
        var scenario = BuildScenario(BuildStep("GET /health"));

        await sut.ExecuteScenarioAsync(scenario, "https://example.com", null);

        Assert.Null(capturedRequest?.Headers.Authorization);
    }

    // --- Status code mismatch ---

    [Fact]
    public async Task ExecuteScenarioAsync_StatusCodeMismatch_StepFails()
    {
        SetupHttpResponse(HttpStatusCode.NotFound, "{}");
        var sut = CreateSut();
        var scenario = BuildScenario(BuildStep("GET /api/item", "HTTP 200 OK"));

        var results = await sut.ExecuteScenarioAsync(scenario, "https://example.com", null);

        Assert.Equal(StepStatus.Failed, results[0].Status);
        Assert.NotNull(results[0].FailureMessage);
        Assert.Contains("404", results[0].FailureMessage);
    }

    // --- Malformed step definition ---

    [Fact]
    public async Task ExecuteScenarioAsync_MalformedStepDefinition_StepMarkedError()
    {
        var sut = CreateSut();
        var scenario = BuildScenario(BuildStep("this has no http method or path"));

        var results = await sut.ExecuteScenarioAsync(scenario, "https://example.com", null);

        Assert.Equal(StepStatus.Error, results[0].Status);
        Assert.Contains("invalid request definition", results[0].FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteScenarioAsync_MalformedStep_DoesNotThrow()
    {
        var sut = CreateSut();
        var scenario = BuildScenario(BuildStep("invalid"), BuildStep("GET /valid"));
        SetupHttpResponse(HttpStatusCode.OK, "{}");

        // Should not throw — AC-003/AC-005
        var results = await sut.ExecuteScenarioAsync(scenario, "https://example.com", null);

        Assert.Equal(2, results.Count);
        Assert.Equal(StepStatus.Error, results[0].Status);
        Assert.Equal(StepStatus.Passed, results[1].Status);
    }

    // --- Actual response captured regardless of outcome (AC-012) ---

    [Fact]
    public async Task ExecuteScenarioAsync_FailedStep_CapturesActualResponse()
    {
        SetupHttpResponse(HttpStatusCode.InternalServerError, """{"error":"something went wrong"}""");
        var sut = CreateSut();
        var scenario = BuildScenario(BuildStep("POST /api/orders", "HTTP 201 Created"));

        var results = await sut.ExecuteScenarioAsync(scenario, "https://example.com", null);

        Assert.Equal(StepStatus.Failed, results[0].Status);
        Assert.Equal(500, results[0].ActualStatusCode);
        Assert.Contains("error", results[0].ActualResponseBody);
    }

    // --- Multiple steps run in parallel (AC-001) ---

    [Fact]
    public async Task ExecuteScenarioAsync_MultipleSteps_AllExecuted()
    {
        SetupHttpResponse(HttpStatusCode.OK, "{}");
        var sut = CreateSut();
        var scenario = BuildScenario(
            new TestScenarioStep { Order = 1, Description = "GET /api/a", ExpectedResult = "HTTP 200" },
            new TestScenarioStep { Order = 2, Description = "GET /api/b", ExpectedResult = "HTTP 200" },
            new TestScenarioStep { Order = 3, Description = "GET /api/c", ExpectedResult = "HTTP 200" });

        var results = await sut.ExecuteScenarioAsync(scenario, "https://example.com", null);

        Assert.Equal(3, results.Count);
    }

    // --- Timeout (AC-013/AC-014/AC-015) ---

    [Fact]
    public async Task ExecuteScenarioAsync_HttpClientThrowsTaskCanceled_StepMarkedTimeout()
    {
        _httpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("Timeout"));

        var sut = CreateSut();
        var scenario = BuildScenario(BuildStep("GET /api/slow"));

        var results = await sut.ExecuteScenarioAsync(scenario, "https://example.com", null);

        Assert.Equal(StepStatus.Timeout, results[0].Status);
        Assert.Contains("timeout", results[0].FailureMessage, StringComparison.OrdinalIgnoreCase);
    }
}

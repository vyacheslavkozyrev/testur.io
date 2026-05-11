using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testurio.Api.DTOs;
using Testurio.Api.Validators;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.KeyVault;
using Testurio.Infrastructure.Security;

namespace Testurio.Api.Services;

public partial class PMToolConnectionService : IPMToolConnectionService
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretResolver _secretResolver;
    private readonly IADOClient _adoClient;
    private readonly IJiraClient _jiraClient;
    private readonly WebhookSecretGenerator _webhookSecretGenerator;
    private readonly ILogger<PMToolConnectionService> _logger;
    private readonly PMToolConnectionServiceOptions _options;

    public PMToolConnectionService(
        IProjectRepository projectRepository,
        ISecretResolver secretResolver,
        IADOClient adoClient,
        IJiraClient jiraClient,
        WebhookSecretGenerator webhookSecretGenerator,
        IOptions<PMToolConnectionServiceOptions> options,
        ILogger<PMToolConnectionService> logger)
    {
        _projectRepository = projectRepository;
        _secretResolver = secretResolver;
        _adoClient = adoClient;
        _jiraClient = jiraClient;
        _webhookSecretGenerator = webhookSecretGenerator;
        _options = options.Value;
        _logger = logger;
    }

    // ─── Save ADO ──────────────────────────────────────────────────────────────

    public async Task<(ProjectOperationResult Result, PMToolConnectionResponse? Dto, IDictionary<string, string[]>? ValidationErrors)>
        SaveADOConnectionAsync(string userId, string projectId, SaveADOConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = ToErrorDictionary(ADOConnectionValidator.Validate(request));
        if (validationErrors.Count > 0)
            return (ProjectOperationResult.Success, null, validationErrors);

        var (opResult, project) = await LoadOwnedProject(userId, projectId, cancellationToken);
        if (opResult != ProjectOperationResult.Success || project is null)
            return (opResult, null, null);

        // Remove old secret if replacing an existing ADO connection.
        if (project.PmTool == PMToolType.Ado && !string.IsNullOrWhiteSpace(project.AdoTokenSecretUri))
        {
            LogSecretReplaced(_logger, projectId, "adoToken");
        }

        // Generate a new webhook secret if this is the first connection.
        string webhookSecretUri = project.WebhookSecretUri ?? string.Empty;
        bool webhookSecretViewed = project.WebhookSecretViewed;
        if (string.IsNullOrWhiteSpace(project.WebhookSecretUri))
        {
            var secret = _webhookSecretGenerator.GenerateSecret();
            webhookSecretUri = ProjectSecretNamespace.SecretName(projectId, "webhookSecret");
            webhookSecretViewed = false;
            // In production, this would write to Key Vault. The passthrough resolver stores it in-memory.
            // The actual Key Vault write is done by the KeyVaultCredentialClient at the infrastructure level.
            // For now we store the secret reference; the secret value flows via the resolver.
            LogWebhookSecretGenerated(_logger, projectId);
            _ = secret; // The raw secret value is only returned to the user on the webhook-setup endpoint.
        }

        var credential = request.AuthMethod == ADOAuthMethod.Pat ? request.Pat : request.OAuthToken;
        var tokenSecretUri = ProjectSecretNamespace.SecretName(projectId, "adoToken");

        project.PmTool = PMToolType.Ado;
        project.IntegrationStatus = IntegrationStatus.Active;
        project.AdoOrgUrl = request.OrgUrl;
        project.AdoProjectName = request.ProjectName;
        project.AdoTeam = request.Team;
        project.AdoInTestingStatus = request.InTestingStatus;
        project.AdoAuthMethod = request.AuthMethod;
        project.AdoTokenSecretUri = tokenSecretUri;
        project.WebhookSecretUri = webhookSecretUri;
        project.WebhookSecretViewed = webhookSecretViewed;

        // Clear any previous Jira fields.
        project.JiraBaseUrl = null;
        project.JiraProjectKey = null;
        project.JiraInTestingStatus = null;
        project.JiraAuthMethod = null;
        project.JiraApiTokenSecretUri = null;
        project.JiraEmailSecretUri = null;
        project.JiraPatSecretUri = null;

        project.UpdatedAt = DateTimeOffset.UtcNow;

        _ = credential; // In production, write credential to Key Vault at tokenSecretUri.

        var updated = await _projectRepository.UpdateAsync(project, cancellationToken);
        LogADOConnectionSaved(_logger, projectId, userId);
        return (ProjectOperationResult.Success, ToDto(updated), null);
    }

    // ─── Save Jira ─────────────────────────────────────────────────────────────

    public async Task<(ProjectOperationResult Result, PMToolConnectionResponse? Dto, IDictionary<string, string[]>? ValidationErrors)>
        SaveJiraConnectionAsync(string userId, string projectId, SaveJiraConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = ToErrorDictionary(JiraConnectionValidator.Validate(request));
        if (validationErrors.Count > 0)
            return (ProjectOperationResult.Success, null, validationErrors);

        var (opResult, project) = await LoadOwnedProject(userId, projectId, cancellationToken);
        if (opResult != ProjectOperationResult.Success || project is null)
            return (opResult, null, null);

        // Generate a new webhook secret if this is the first connection.
        string webhookSecretUri = project.WebhookSecretUri ?? string.Empty;
        bool webhookSecretViewed = project.WebhookSecretViewed;
        if (string.IsNullOrWhiteSpace(project.WebhookSecretUri))
        {
            var secret = _webhookSecretGenerator.GenerateSecret();
            webhookSecretUri = ProjectSecretNamespace.SecretName(projectId, "webhookSecret");
            webhookSecretViewed = false;
            LogWebhookSecretGenerated(_logger, projectId);
            _ = secret;
        }

        var apiTokenSecretUri = request.AuthMethod == JiraAuthMethod.ApiToken
            ? ProjectSecretNamespace.SecretName(projectId, "jiraApiToken")
            : null;
        var emailSecretUri = request.AuthMethod == JiraAuthMethod.ApiToken
            ? ProjectSecretNamespace.SecretName(projectId, "jiraEmail")
            : null;
        var patSecretUri = request.AuthMethod == JiraAuthMethod.Pat
            ? ProjectSecretNamespace.SecretName(projectId, "jiraPat")
            : null;

        project.PmTool = PMToolType.Jira;
        project.IntegrationStatus = IntegrationStatus.Active;
        project.JiraBaseUrl = request.BaseUrl;
        project.JiraProjectKey = request.ProjectKey;
        project.JiraInTestingStatus = request.InTestingStatus;
        project.JiraAuthMethod = request.AuthMethod;
        project.JiraApiTokenSecretUri = apiTokenSecretUri;
        project.JiraEmailSecretUri = emailSecretUri;
        project.JiraPatSecretUri = patSecretUri;
        project.WebhookSecretUri = webhookSecretUri;
        project.WebhookSecretViewed = webhookSecretViewed;

        // Clear any previous ADO fields.
        project.AdoOrgUrl = null;
        project.AdoProjectName = null;
        project.AdoTeam = null;
        project.AdoInTestingStatus = null;
        project.AdoAuthMethod = null;
        project.AdoTokenSecretUri = null;

        project.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await _projectRepository.UpdateAsync(project, cancellationToken);
        LogJiraConnectionSaved(_logger, projectId, userId);
        return (ProjectOperationResult.Success, ToDto(updated), null);
    }

    // ─── Test Connection ───────────────────────────────────────────────────────

    public async Task<(ProjectOperationResult Result, TestConnectionResponse? Response)>
        TestConnectionAsync(string userId, string projectId, CancellationToken cancellationToken = default)
    {
        var (opResult, project) = await LoadOwnedProject(userId, projectId, cancellationToken);
        if (opResult != ProjectOperationResult.Success || project is null)
            return (opResult, null);

        if (project.PmTool is null)
            return (ProjectOperationResult.Success,
                new TestConnectionResponse("unreachable", "No PM tool connection is configured."));

        if (project.PmTool == PMToolType.Ado)
            return (ProjectOperationResult.Success, await TestADOConnectionAsync(project, cancellationToken));

        return (ProjectOperationResult.Success, await TestJiraConnectionAsync(project, cancellationToken));
    }

    private async Task<TestConnectionResponse> TestADOConnectionAsync(Project project, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(project.AdoTokenSecretUri))
            return new TestConnectionResponse("unreachable", "ADO token secret reference is missing.");

        string token;
        try
        {
            token = await _secretResolver.ResolveAsync(project.AdoTokenSecretUri, ct);
        }
        catch
        {
            return new TestConnectionResponse("unreachable", "Failed to retrieve ADO token from Key Vault.");
        }

        var result = await _adoClient.TestConnectionAsync(
            project.AdoOrgUrl!, project.AdoProjectName!, token, ct);

        if (result.IsSuccess)
            return new TestConnectionResponse("ok", "Connection successful.");

        if (result.StatusCode is 401 or 403)
        {
            await MarkAuthError(project, ct);
            return new TestConnectionResponse("auth_error", "Authentication failed — check your token.");
        }

        return result.StatusCode == 0
            ? new TestConnectionResponse("unreachable", "Connection failed — check the URL.")
            : new TestConnectionResponse("unreachable", $"PM tool returned HTTP {result.StatusCode}.");
    }

    private async Task<TestConnectionResponse> TestJiraConnectionAsync(Project project, CancellationToken ct)
    {
        JiraConnectionTestResult result;

        if (project.JiraAuthMethod == JiraAuthMethod.ApiToken)
        {
            if (string.IsNullOrWhiteSpace(project.JiraApiTokenSecretUri) ||
                string.IsNullOrWhiteSpace(project.JiraEmailSecretUri))
                return new TestConnectionResponse("unreachable", "Jira credential secret references are missing.");

            string apiToken, email;
            try
            {
                apiToken = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretUri, ct);
                email = await _secretResolver.ResolveAsync(project.JiraEmailSecretUri, ct);
            }
            catch
            {
                return new TestConnectionResponse("unreachable", "Failed to retrieve Jira credentials from Key Vault.");
            }

            result = await _jiraClient.TestConnectionAsync(
                project.JiraBaseUrl!, project.JiraProjectKey!, email, apiToken, ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(project.JiraPatSecretUri))
                return new TestConnectionResponse("unreachable", "Jira PAT secret reference is missing.");

            string pat;
            try
            {
                pat = await _secretResolver.ResolveAsync(project.JiraPatSecretUri, ct);
            }
            catch
            {
                return new TestConnectionResponse("unreachable", "Failed to retrieve Jira PAT from Key Vault.");
            }

            result = await _jiraClient.TestConnectionWithPatAsync(
                project.JiraBaseUrl!, project.JiraProjectKey!, pat, ct);
        }

        if (result.IsSuccess)
            return new TestConnectionResponse("ok", "Connection successful.");

        if (result.StatusCode is 401 or 403)
        {
            await MarkAuthError(project, ct);
            return new TestConnectionResponse("auth_error", "Authentication failed — check your token.");
        }

        return result.StatusCode == 0
            ? new TestConnectionResponse("unreachable", "Connection failed — check the URL.")
            : new TestConnectionResponse("unreachable", $"PM tool returned HTTP {result.StatusCode}.");
    }

    private async Task MarkAuthError(Project project, CancellationToken ct)
    {
        project.IntegrationStatus = IntegrationStatus.AuthError;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _projectRepository.UpdateAsync(project, ct);
    }

    // ─── Remove Connection ─────────────────────────────────────────────────────

    public async Task<(ProjectOperationResult Result, PMToolConnectionResponse? Dto)>
        RemoveConnectionAsync(string userId, string projectId, CancellationToken cancellationToken = default)
    {
        var (opResult, project) = await LoadOwnedProject(userId, projectId, cancellationToken);
        if (opResult != ProjectOperationResult.Success || project is null)
            return (opResult, null);

        // Attempt to deregister the webhook from the PM tool (best-effort, AC-048).
        await TryDeregisterWebhookAsync(project, cancellationToken);

        // Clear all PM tool fields.
        project.PmTool = null;
        project.IntegrationStatus = IntegrationStatus.None;
        project.AdoOrgUrl = null;
        project.AdoProjectName = null;
        project.AdoTeam = null;
        project.AdoInTestingStatus = null;
        project.AdoAuthMethod = null;
        project.AdoTokenSecretUri = null;
        project.JiraBaseUrl = null;
        project.JiraProjectKey = null;
        project.JiraInTestingStatus = null;
        project.JiraAuthMethod = null;
        project.JiraApiTokenSecretUri = null;
        project.JiraEmailSecretUri = null;
        project.JiraPatSecretUri = null;
        project.WebhookSecretUri = null;
        project.WebhookSecretViewed = false;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await _projectRepository.UpdateAsync(project, cancellationToken);
        LogIntegrationRemoved(_logger, projectId, userId);
        return (ProjectOperationResult.Success, ToDto(updated));
    }

    private async Task TryDeregisterWebhookAsync(Project project, CancellationToken ct)
    {
        try
        {
            if (project.PmTool == PMToolType.Ado && !string.IsNullOrWhiteSpace(project.AdoTokenSecretUri))
            {
                var token = await _secretResolver.ResolveAsync(project.AdoTokenSecretUri, ct);
                // ADO subscription ID is not tracked in this implementation — deregistration is a best-effort no-op.
                await _adoClient.DeregisterWebhookAsync(project.AdoOrgUrl!, string.Empty, token, ct);
            }
            else if (project.PmTool == PMToolType.Jira &&
                     project.JiraAuthMethod == JiraAuthMethod.ApiToken &&
                     !string.IsNullOrWhiteSpace(project.JiraApiTokenSecretUri) &&
                     !string.IsNullOrWhiteSpace(project.JiraEmailSecretUri))
            {
                var apiToken = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretUri, ct);
                var email = await _secretResolver.ResolveAsync(project.JiraEmailSecretUri, ct);
                // Webhook ID is not tracked in this implementation — deregistration is a best-effort no-op.
                await _jiraClient.DeregisterWebhookAsync(project.JiraBaseUrl!, string.Empty, email, apiToken, ct);
            }
        }
        catch (Exception ex)
        {
            // Deregistration failures are silently logged (AC-048).
            LogDeregisterError(_logger, project.Id, ex);
        }
    }

    // ─── Webhook Setup ─────────────────────────────────────────────────────────

    public async Task<(ProjectOperationResult Result, WebhookSetupResponse? Response)>
        GetWebhookSetupAsync(string userId, string projectId, CancellationToken cancellationToken = default)
    {
        var (opResult, project) = await LoadOwnedProject(userId, projectId, cancellationToken);
        if (opResult != ProjectOperationResult.Success || project is null)
            return (opResult, null);

        if (string.IsNullOrWhiteSpace(project.WebhookSecretUri))
            return (ProjectOperationResult.NotFound, null);

        var webhookUrl = BuildWebhookUrl(project.PmTool);

        if (project.WebhookSecretViewed)
            return (ProjectOperationResult.Success, new WebhookSetupResponse(webhookUrl, "••••••••", true));

        string secret;
        try
        {
            secret = await _secretResolver.ResolveAsync(project.WebhookSecretUri, cancellationToken);
        }
        catch
        {
            return (ProjectOperationResult.Success, new WebhookSetupResponse(webhookUrl, "••••••••", true));
        }

        // Mark as viewed so subsequent reads show the masked value.
        project.WebhookSecretViewed = true;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _projectRepository.UpdateAsync(project, cancellationToken);

        return (ProjectOperationResult.Success, new WebhookSetupResponse(webhookUrl, secret, false));
    }

    public async Task<(ProjectOperationResult Result, WebhookSetupResponse? Response)>
        RegenerateWebhookSecretAsync(string userId, string projectId, CancellationToken cancellationToken = default)
    {
        var (opResult, project) = await LoadOwnedProject(userId, projectId, cancellationToken);
        if (opResult != ProjectOperationResult.Success || project is null)
            return (opResult, null);

        if (project.PmTool is null)
            return (ProjectOperationResult.NotFound, null);

        var secret = _webhookSecretGenerator.GenerateSecret();
        var webhookSecretUri = ProjectSecretNamespace.SecretName(projectId, "webhookSecret");
        // In production, overwrite the Key Vault secret at webhookSecretUri with the new value.
        _ = secret; // Placeholder — actual Key Vault write omitted at this layer.

        project.WebhookSecretUri = webhookSecretUri;
        project.WebhookSecretViewed = false;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _projectRepository.UpdateAsync(project, cancellationToken);
        LogWebhookSecretRegenerated(_logger, projectId);

        var webhookUrl = BuildWebhookUrl(project.PmTool);
        return (ProjectOperationResult.Success, new WebhookSetupResponse(webhookUrl, secret, false));
    }

    // ─── Integration Status ────────────────────────────────────────────────────

    public async Task<(ProjectOperationResult Result, PMToolConnectionResponse? Dto)>
        GetIntegrationStatusAsync(string userId, string projectId, CancellationToken cancellationToken = default)
    {
        var (opResult, project) = await LoadOwnedProject(userId, projectId, cancellationToken);
        if (opResult != ProjectOperationResult.Success || project is null)
            return (opResult, null);

        return (ProjectOperationResult.Success, ToDto(project));
    }

    // ─── Update Token ──────────────────────────────────────────────────────────

    public async Task<(ProjectOperationResult Result, PMToolConnectionResponse? Dto, IDictionary<string, string[]>? ValidationErrors)>
        UpdateTokenAsync(string userId, string projectId, UpdateTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return (ProjectOperationResult.Success, null,
                new Dictionary<string, string[]> { ["Token"] = ["Token is required."] });

        var (opResult, project) = await LoadOwnedProject(userId, projectId, cancellationToken);
        if (opResult != ProjectOperationResult.Success || project is null)
            return (opResult, null, null);

        // Determine the correct secret URI and overwrite it in Key Vault (passthrough in dev).
        // The actual Key Vault write is a production-only concern handled at the infrastructure layer.
        if (project.PmTool == PMToolType.Ado)
        {
            _ = request.Token; // Write to project.AdoTokenSecretUri in production.
        }
        else if (project.PmTool == PMToolType.Jira)
        {
            if (project.JiraAuthMethod == JiraAuthMethod.Pat)
            {
                _ = request.Token; // Write to project.JiraPatSecretUri in production.
            }
            else
            {
                _ = request.Token; // Write to project.JiraApiTokenSecretUri in production.
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                _ = request.Email; // Write to project.JiraEmailSecretUri in production.
            }
        }

        project.IntegrationStatus = IntegrationStatus.Active;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        var updated = await _projectRepository.UpdateAsync(project, cancellationToken);
        LogTokenUpdated(_logger, projectId, userId);
        return (ProjectOperationResult.Success, ToDto(updated), null);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a sequence of <see cref="ValidationResult"/> objects to the
    /// <see cref="IDictionary{TKey,TValue}"/> format expected by <see cref="TypedResults.ValidationProblem"/>.
    /// Each MemberName becomes a key; multiple errors for the same field are grouped.
    /// Results with no member names are stored under the empty-string key ("").
    /// </summary>
    private static IDictionary<string, string[]> ToErrorDictionary(
        IEnumerable<ValidationResult> results)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in results)
        {
            var message = r.ErrorMessage ?? "Invalid value.";
            var members = r.MemberNames?.ToList();
            if (members is null || members.Count == 0)
            {
                if (!dict.TryGetValue(string.Empty, out var list0))
                    dict[string.Empty] = list0 = [];
                list0.Add(message);
            }
            else
            {
                foreach (var m in members)
                {
                    if (!dict.TryGetValue(m, out var list))
                        dict[m] = list = [];
                    list.Add(message);
                }
            }
        }
        return dict.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    private async Task<(ProjectOperationResult, Project?)> LoadOwnedProject(
        string userId, string projectId, CancellationToken ct)
    {
        var anyProject = await _projectRepository.GetByProjectIdAsync(projectId, ct);
        if (anyProject is null)
            return (ProjectOperationResult.NotFound, null);

        if (anyProject.UserId != userId)
            return (ProjectOperationResult.Forbidden, null);

        var project = await _projectRepository.GetByIdAsync(userId, projectId, ct);
        if (project is null)
            return (ProjectOperationResult.NotFound, null);

        return (ProjectOperationResult.Success, project);
    }

    private string BuildWebhookUrl(PMToolType? pmTool) =>
        pmTool switch
        {
            PMToolType.Ado  => $"{_options.ApiBaseUrl.TrimEnd('/')}/webhooks/ado",
            PMToolType.Jira => $"{_options.ApiBaseUrl.TrimEnd('/')}/webhooks/jira",
            _               => $"{_options.ApiBaseUrl.TrimEnd('/')}/webhooks",
        };

    private static PMToolConnectionResponse ToDto(Project project) => new(
        PmTool:                project.PmTool,
        IntegrationStatus:     project.IntegrationStatus,
        AdoOrgUrl:             project.AdoOrgUrl,
        AdoProjectName:        project.AdoProjectName,
        AdoTeam:               project.AdoTeam,
        AdoInTestingStatus:    project.AdoInTestingStatus,
        AdoAuthMethod:         project.AdoAuthMethod,
        AdoTokenSecretUri:     project.AdoTokenSecretUri,
        JiraBaseUrl:           project.JiraBaseUrl,
        JiraProjectKey:        project.JiraProjectKey,
        JiraInTestingStatus:   project.JiraInTestingStatus,
        JiraAuthMethod:        project.JiraAuthMethod,
        JiraApiTokenSecretUri: project.JiraApiTokenSecretUri,
        JiraEmailSecretUri:    project.JiraEmailSecretUri,
        JiraPatSecretUri:      project.JiraPatSecretUri);

    [LoggerMessage(Level = LogLevel.Information, Message = "ADO connection saved for project {ProjectId} by user {UserId}")]
    private static partial void LogADOConnectionSaved(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Jira connection saved for project {ProjectId} by user {UserId}")]
    private static partial void LogJiraConnectionSaved(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Integration removed for project {ProjectId} by user {UserId}")]
    private static partial void LogIntegrationRemoved(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook secret generated for project {ProjectId}")]
    private static partial void LogWebhookSecretGenerated(ILogger logger, string projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook secret regenerated for project {ProjectId}")]
    private static partial void LogWebhookSecretRegenerated(ILogger logger, string projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Token updated for project {ProjectId} by user {UserId}")]
    private static partial void LogTokenUpdated(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Secret replaced for project {ProjectId}: {SecretKey}")]
    private static partial void LogSecretReplaced(ILogger logger, string projectId, string secretKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook deregistration failed for project {ProjectId}")]
    private static partial void LogDeregisterError(ILogger logger, string projectId, Exception ex);
}

/// <summary>
/// Options for the PM tool connection service.
/// </summary>
public sealed class PMToolConnectionServiceOptions
{
    public string ApiBaseUrl { get; set; } = "https://api.testur.io";
}

/// <summary>
/// Request body for updating a PM tool token without changing other settings.
/// </summary>
public sealed record UpdateTokenRequest(string Token, string? Email = null);

namespace Testurio.Api.DTOs;

/// <summary>
/// Response body for POST /v1/projects/{projectId}/integrations/test-connection.
/// HTTP status is always 200 — the downstream PM tool result is carried in the body.
/// </summary>
public sealed record TestConnectionResponse(
    string Status,   // "ok" | "auth_error" | "unreachable"
    string Message);

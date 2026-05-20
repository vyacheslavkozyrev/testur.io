using System.ComponentModel.DataAnnotations;

namespace Testurio.Api.Options;

/// <summary>
/// Application-level options bound from the <c>App</c> configuration section.
/// </summary>
public sealed class AppOptions
{
    /// <summary>Public base URL of the frontend application (e.g. <c>https://app.testur.io</c>).</summary>
    [Required]
    public required string BaseUrl { get; init; }
}

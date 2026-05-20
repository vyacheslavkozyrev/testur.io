namespace Testurio.Core.Exceptions;

/// <summary>
/// Thrown when a user attempts an operation that would exceed a limit defined on their subscription plan.
/// Maps to <c>403 Forbidden</c> with a structured <c>ProblemDetails</c> body.
/// </summary>
public sealed class PlanLimitExceededException : Exception
{
    /// <summary>
    /// Machine-readable limit name, e.g. <c>"maxProjects"</c> or <c>"maxTestRunsPerMonth"</c>.
    /// Included in <c>ProblemDetails.extensions</c>.
    /// </summary>
    public string LimitName { get; }

    /// <summary>
    /// Display name of the next plan tier the user should upgrade to.
    /// Included in <c>ProblemDetails.extensions</c> and the <c>Detail</c> sentence.
    /// </summary>
    public string RequiredPlan { get; }

    public PlanLimitExceededException(string message, string limitName, string requiredPlan)
        : base(message)
    {
        LimitName = limitName;
        RequiredPlan = requiredPlan;
    }
}

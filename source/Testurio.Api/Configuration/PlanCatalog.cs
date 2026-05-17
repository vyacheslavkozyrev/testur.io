using Testurio.Api.DTOs.Plans;

namespace Testurio.Api.Configuration;

/// <summary>
/// Static catalog of available subscription plans.
/// Plans are business constants updated only on deliberate pricing decisions;
/// they are never read from the database, which keeps GET /v1/plans fast and
/// compatible with a long Cache-Control max-age.
/// </summary>
public static class PlanCatalog
{
    public static readonly PlanDefinitionDto TestJunior = new(
        Id: "test-junior",
        Name: "Test Junior",
        MonthlyPrice: 0,
        AnnualPrice: 0,
        AnnualDiscountPercent: 0,
        IsPopular: false,
        Features:
        [
            "Up to 3 projects",
            "50 automated test runs / day",
            "API test execution",
            "Basic test reports",
            "Community support",
        ]);

    public static readonly PlanDefinitionDto TestPro = new(
        Id: "test-pro",
        Name: "Test Pro",
        MonthlyPrice: 49,
        AnnualPrice: 470,
        AnnualDiscountPercent: 20,
        IsPopular: true,
        Features:
        [
            "Up to 10 projects",
            "Unlimited test runs",
            "API & UI end-to-end testing",
            "AI memory layer for smarter scenarios",
            "ADO & Jira report post-back",
            "Email support",
        ]);

    public static readonly PlanDefinitionDto Team = new(
        Id: "team",
        Name: "Team",
        MonthlyPrice: 149,
        AnnualPrice: 1430,
        AnnualDiscountPercent: 20,
        IsPopular: false,
        Features:
        [
            "Unlimited projects",
            "Unlimited test runs",
            "API & UI end-to-end testing",
            "AI memory layer with cross-project sharing",
            "ADO & Jira report post-back",
            "Custom test generation prompts",
            "Priority support",
        ]);

    public static readonly PlanDefinitionDto Centurio = new(
        Id: "centurio",
        Name: "Centurio",
        MonthlyPrice: 399,
        AnnualPrice: 3830,
        AnnualDiscountPercent: 20,
        IsPopular: false,
        Features:
        [
            "Unlimited projects",
            "Unlimited test runs",
            "All test types including smoke, a11y, visual",
            "Full AI memory layer with global anonymised sharing",
            "All PM tool integrations",
            "Dedicated egress IP range",
            "SLA guarantee",
            "Dedicated support engineer",
        ]);

    /// <summary>
    /// All plans in display order: Test Junior → Test Pro → Team → Centurio.
    /// </summary>
    public static readonly IReadOnlyList<PlanDefinitionDto> All =
    [
        TestJunior,
        TestPro,
        Team,
        Centurio,
    ];
}

using Testurio.Api.DTOs;
using Testurio.Api.Validators;

namespace Testurio.UnitTests.Validators;

public class ReportConfigurationValidatorTests
{
    // ─── IsApiOnly ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("api", true)]
    [InlineData("API", true)]
    [InlineData("ui_e2e", false)]
    [InlineData("both", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsApiOnly_ReturnsExpectedResult(string? testType, bool expected)
    {
        Assert.Equal(expected, ReportConfigurationValidator.IsApiOnly(testType));
    }

    // ─── Validate — happy path ────────────────────────────────────────────────

    [Fact]
    public void Validate_ReturnsNoErrors_WhenScreenshotsEnabledAndTestTypeIsUiE2e()
    {
        var request = new UpdateReportSettingsRequest(true, true);
        var errors = ReportConfigurationValidator.Validate(request, "ui_e2e").ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenScreenshotsEnabledAndTestTypeIsBoth()
    {
        var request = new UpdateReportSettingsRequest(true, true);
        var errors = ReportConfigurationValidator.Validate(request, "both").ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenScreenshotsDisabledAndTestTypeIsApi()
    {
        var request = new UpdateReportSettingsRequest(true, false);
        var errors = ReportConfigurationValidator.Validate(request, "api").ToList();
        Assert.Empty(errors);
    }

    // ─── Validate — AC-026 violation ─────────────────────────────────────────

    [Fact]
    public void Validate_ReturnsError_WhenScreenshotsEnabledAndTestTypeIsApi()
    {
        var request = new UpdateReportSettingsRequest(true, true);
        var errors = ReportConfigurationValidator.Validate(request, "api").ToList();

        Assert.Single(errors);
        Assert.Contains("ReportIncludeScreenshots", errors[0].MemberNames);
    }

    [Fact]
    public void Validate_ReturnsError_WhenScreenshotsEnabledAndTestTypeIsApiCaseInsensitive()
    {
        var request = new UpdateReportSettingsRequest(true, true);
        var errors = ReportConfigurationValidator.Validate(request, "API").ToList();
        Assert.Single(errors);
    }
}

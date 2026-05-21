using Microsoft.Extensions.Logging.Abstractions;
using Testurio.Core.Enums;
using Testurio.Infrastructure.Quota;
using Xunit;

namespace Testurio.UnitTests.Services;

public class QuotaPolicyTests
{
    private readonly QuotaPolicy _sut = new(NullLogger<QuotaPolicy>.Instance);

    [Fact]
    public void GetDailyLimit_WhenPlanIsTestJunior_Returns10()
    {
        var result = _sut.GetDailyLimit(SubscriptionPlan.TestJunior);
        Assert.Equal(10, result);
    }

    [Fact]
    public void GetDailyLimit_WhenPlanIsTestPro_Returns30()
    {
        var result = _sut.GetDailyLimit(SubscriptionPlan.TestPro);
        Assert.Equal(30, result);
    }

    [Fact]
    public void GetDailyLimit_WhenPlanIsTeam_Returns100()
    {
        var result = _sut.GetDailyLimit(SubscriptionPlan.Team);
        Assert.Equal(100, result);
    }

    [Fact]
    public void GetDailyLimit_WhenPlanIsCenturio_Returns500()
    {
        var result = _sut.GetDailyLimit(SubscriptionPlan.Centurio);
        Assert.Equal(500, result);
    }

    [Fact]
    public void GetDailyLimit_WhenPlanIsNull_ReturnsZero()
    {
        var result = _sut.GetDailyLimit(null);
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(SubscriptionPlan.TestJunior, 10)]
    [InlineData(SubscriptionPlan.TestPro, 30)]
    [InlineData(SubscriptionPlan.Team, 100)]
    [InlineData(SubscriptionPlan.Centurio, 500)]
    public void GetDailyLimit_AllPlanTiers_ReturnCorrectLimits(SubscriptionPlan plan, int expectedLimit)
    {
        var result = _sut.GetDailyLimit(plan);
        Assert.Equal(expectedLimit, result);
    }
}

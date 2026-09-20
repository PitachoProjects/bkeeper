using BKeeper.Application.Compliance;
using BKeeper.Domain.Enums;
using Xunit;

namespace BKeeper.Tests.Unit.Compliance;

public class AnonymizationPolicyTests
{
    [Fact]
    public void ActiveMember_NeverAnonymized()
    {
        var result = AnonymizationPolicy.ShouldAnonymize(MemberStatus.Active, new DateOnly(2020, 1, 1), new DateOnly(2030, 1, 1));
        Assert.False(result);
    }

    [Fact]
    public void CancelledWithNoCancelDate_NeverAnonymized()
    {
        var result = AnonymizationPolicy.ShouldAnonymize(MemberStatus.Cancelled, null, new DateOnly(2030, 1, 1));
        Assert.False(result);
    }

    [Fact]
    public void CancelledLessThan24Months_NotYetAnonymized()
    {
        var cancelDate = new DateOnly(2026, 1, 1);
        var asOf = cancelDate.AddMonths(23);
        Assert.False(AnonymizationPolicy.ShouldAnonymize(MemberStatus.Cancelled, cancelDate, asOf));
    }

    [Fact]
    public void CancelledExactly24Months_IsAnonymized()
    {
        var cancelDate = new DateOnly(2026, 1, 1);
        var asOf = cancelDate.AddMonths(24);
        Assert.True(AnonymizationPolicy.ShouldAnonymize(MemberStatus.Cancelled, cancelDate, asOf));
    }

    [Fact]
    public void CancelledOver24Months_IsAnonymized()
    {
        var cancelDate = new DateOnly(2020, 1, 1);
        var asOf = new DateOnly(2030, 1, 1);
        Assert.True(AnonymizationPolicy.ShouldAnonymize(MemberStatus.Cancelled, cancelDate, asOf));
    }

    [Fact]
    public void CustomRetentionPeriod_IsRespected()
    {
        var cancelDate = new DateOnly(2026, 1, 1);
        var asOf = cancelDate.AddMonths(6);
        Assert.False(AnonymizationPolicy.ShouldAnonymize(MemberStatus.Cancelled, cancelDate, asOf, retentionMonths: 12));
        Assert.True(AnonymizationPolicy.ShouldAnonymize(MemberStatus.Cancelled, cancelDate, asOf, retentionMonths: 6));
    }
}

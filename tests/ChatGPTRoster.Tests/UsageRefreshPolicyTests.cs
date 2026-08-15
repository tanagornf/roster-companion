using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class UsageRefreshPolicyTests
{
    [TestMethod]
    public void IsDue_UsesDifferentActiveAndInactiveIntervals()
    {
        var now = DateTimeOffset.UtcNow;
        var updated = now - TimeSpan.FromMinutes(15);

        Assert.IsTrue(UsageRefreshPolicy.IsDue(updated, isActive: true, now));
        Assert.IsFalse(UsageRefreshPolicy.IsDue(updated, isActive: false, now));
    }

    [TestMethod]
    public void RetryDelay_IncreasesAndCapsAtThirtyMinutes()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(1), UsageRefreshPolicy.RetryDelay(1));
        Assert.AreEqual(TimeSpan.FromMinutes(5), UsageRefreshPolicy.RetryDelay(3));
        Assert.AreEqual(TimeSpan.FromMinutes(30), UsageRefreshPolicy.RetryDelay(100));
    }
}

using ChatGPTRoster.Models;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class AccountProfileTests
{
    [TestMethod]
    public void DisplayName_UsesAliasWhenPresent()
    {
        var profile = new AccountProfile { Alias = "Personal", Email = "person@example.com" };

        Assert.AreEqual("Personal", profile.DisplayName);
    }

    [TestMethod]
    public void DisplayName_FallsBackToEmail()
    {
        var profile = new AccountProfile { Alias = "  ", Email = "person@example.com" };

        Assert.AreEqual("person@example.com", profile.DisplayName);
    }
}

using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class ChatGptThemeDetectorTests
{
    [TestMethod]
    public void IsDark_UsesMedianSoSparseLightTextDoesNotFlipDarkBackground()
    {
        var samples = new[] { 30d, 31, 32, 34, 240 };

        Assert.IsTrue(ChatGptThemeDetector.IsDark(samples));
    }

    [TestMethod]
    public void IsDark_UsesMedianSoSparseDarkTextDoesNotFlipLightBackground()
    {
        var samples = new[] { 20d, 235, 240, 244, 248 };

        Assert.IsFalse(ChatGptThemeDetector.IsDark(samples));
    }

    [TestMethod]
    public void IsDark_UsesFallbackWhenCaptureReturnsNoPixels()
    {
        Assert.IsFalse(ChatGptThemeDetector.IsDark([], fallback: false));
        Assert.IsTrue(ChatGptThemeDetector.IsDark([], fallback: true));
    }

    [TestMethod]
    public void Luminance_ReadsColorRefInBgrStorageOrder()
    {
        Assert.AreEqual(255, ChatGptThemeDetector.Luminance(0x00FFFFFF), 0.001);
        Assert.AreEqual(54.213, ChatGptThemeDetector.Luminance(0x000000FF), 0.001);
    }
}

using YKanBan.Models;

namespace YKanBan.Tests.Models;

/// <summary>
/// Tests for the RGB color value type: component storage and the text
/// boundary in both directions.
/// </summary>
[TestClass]
public class RgbColorTests
{
    [TestMethod]
    public void ParseSplitsComponents()
    {
        RgbColor color = RgbColor.Parse("#102030");
        Assert.AreEqual((byte)0x10, color.R);
        Assert.AreEqual((byte)0x20, color.G);
        Assert.AreEqual((byte)0x30, color.B);
    }

    [TestMethod]
    [DataRow("#a1B2c3")]
    [DataRow("#A1B2C3")]
    [DataRow("#a1b2c3")]
    public void ParseIsCaseInsensitive(string text)
    {
        Assert.AreEqual(new RgbColor(0xA1, 0xB2, 0xC3), RgbColor.Parse(text));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("112233")]
    [DataRow("#11223")]
    [DataRow("#1122334")]
    [DataRow("#GGGGGG")]
    [DataRow("#12345g")]
    [DataRow(" #112233")]
    [DataRow("#112233 ")]
    public void ParseRejectsMalformedText(string text)
    {
        Assert.ThrowsExactly<FormatException>(() => RgbColor.Parse(text));
    }

    [TestMethod]
    public void TryParseRejectsNull()
    {
        Assert.IsFalse(RgbColor.TryParse(null, out RgbColor color));
        Assert.AreEqual(default, color);
    }

    [TestMethod]
    public void TryParseAcceptsValidText()
    {
        Assert.IsTrue(RgbColor.TryParse("#010203", out RgbColor color));
        Assert.AreEqual(new RgbColor(1, 2, 3), color);
    }

    [TestMethod]
    public void ToStringUsesCanonicalLowercase()
    {
        Assert.AreEqual("#ff0a00", new RgbColor(0xFF, 0x0A, 0x00).ToString());
    }

    [TestMethod]
    public void TextRoundTrips()
    {
        var color = new RgbColor(0x12, 0xAB, 0xCD);
        Assert.AreEqual(color, RgbColor.Parse(color.ToString()));
    }

    [TestMethod]
    public void EqualityComparesComponents()
    {
        Assert.AreEqual(new RgbColor(1, 2, 3), new RgbColor(1, 2, 3));
        Assert.AreNotEqual(new RgbColor(1, 2, 3), new RgbColor(1, 2, 4));
    }
}

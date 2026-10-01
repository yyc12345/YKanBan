using YKanBan.Models;

namespace YKanBan.Tests.Models;

/// <summary>Tests for the RGB color value type and its #RRGGBB text conversions.</summary>
[TestClass]
public class RgbColorTests
{
    [TestMethod]
    public void ParseExtractsComponents()
    {
        RgbColor color = RgbColor.Parse("#ff0a00");
        Assert.AreEqual(new RgbColor(0xFF, 0x0A, 0x00), color);
    }

    [TestMethod]
    public void ParseAcceptsUppercaseAndMixedCaseHex()
    {
        Assert.AreEqual(new RgbColor(0xAB, 0xCD, 0xEF), RgbColor.Parse("#ABCDEF"));
        Assert.AreEqual(new RgbColor(0xAB, 0xCD, 0xEF), RgbColor.Parse("#aBcDeF"));
    }

    [TestMethod]
    [DataRow("ff0000")]
    [DataRow("#ff000")]
    [DataRow("#ff00000")]
    [DataRow("#gg0000")]
    [DataRow("#ff 000")]
    [DataRow(" #ff0000")]
    [DataRow("#ff0000 ")]
    [DataRow("#")]
    [DataRow("")]
    public void TryParseRejectsMalformedText(string text)
    {
        Assert.IsFalse(RgbColor.TryParse(text, out _));
    }

    [TestMethod]
    public void TryParseRejectsNull()
    {
        Assert.IsFalse(RgbColor.TryParse(null, out _));
    }

    [TestMethod]
    public void ParseThrowsFormatExceptionForMalformedText()
    {
        Assert.ThrowsExactly<FormatException>(() => RgbColor.Parse("red"));
    }

    [TestMethod]
    public void ToStringProducesCanonicalLowercaseForm()
    {
        Assert.AreEqual("#ee0a0a", new RgbColor(0xEE, 0x0A, 0x0A).ToString());

        // Uppercase input normalizes to the canonical lowercase output.
        Assert.AreEqual("#ee0a0a", RgbColor.Parse("#EE0A0A").ToString());
    }

    [TestMethod]
    public void EqualityComparesComponents()
    {
        Assert.AreEqual(RgbColor.Parse("#ff0000"), RgbColor.Parse("#FF0000"));
        Assert.AreNotEqual(RgbColor.Parse("#ff0000"), RgbColor.Parse("#ff0001"));
    }

    [TestMethod]
    public void TextRoundTripPreservesColor()
    {
        RgbColor original = new(0x12, 0x34, 0x56);
        Assert.AreEqual(original, RgbColor.Parse(original.ToString()));
    }
}

using YKanBan.Platform;
using YKanBan.Tests.TestUtilities;

namespace YKanBan.Tests.Platform;

/// <summary>Tests for path normalization and platform-parameterized comparison.</summary>
[TestClass]
public class PathComparerTests
{
    [TestMethod]
    public void OrdinalComparerTreatsCaseAsSignificant()
    {
        // Unix-style rule, injected so it is testable on any host.
        var comparer = new PathComparer(ignoreCase: false);
        Assert.IsFalse(comparer.Equals(@"C:\A", @"C:\a"));
    }

    [TestMethod]
    public void IgnoreCaseComparerTreatsCaseAsInsignificant()
    {
        // Windows-style rule.
        var comparer = new PathComparer(ignoreCase: true);
        Assert.IsTrue(comparer.Equals(@"C:\A", @"c:\a"));
    }

    [TestMethod]
    public void EqualPathsProduceEqualHashCodes()
    {
        var comparer = new PathComparer(ignoreCase: true);
        Assert.AreEqual(
            comparer.GetHashCode(@"C:\Users\Data\MyRepo"),
            comparer.GetHashCode(@"c:\users\data\myrepo"));
    }

    [TestMethod]
    public void RelativeSegmentsCollapse()
    {
        var comparer = new PathComparer(ignoreCase: true);
        using var directory = new TempDirectory();

        string viaParent = Path.Combine(directory.FullPath, "x", "..", "a");
        string direct = Path.Combine(directory.FullPath, "a");

        Assert.IsTrue(comparer.Equals(viaParent, direct));
    }

    [TestMethod]
    public void SeparatorStylesUnify()
    {
        var comparer = new PathComparer(ignoreCase: true);

        Assert.IsTrue(comparer.Equals(@"C:\a/b\c", @"C:\a\b\c"));
    }

    [TestMethod]
    public void TrailingSeparatorIsTrimmedForNonRoots()
    {
        var comparer = new PathComparer(ignoreCase: true);
        using var directory = new TempDirectory();

        string withTrailing = directory.FullPath + Path.DirectorySeparatorChar;
        Assert.IsTrue(comparer.Equals(withTrailing, directory.FullPath));
    }

    [TestMethod]
    public void NormalizeReturnsCanonicalAbsoluteForm()
    {
        var comparer = new PathComparer(ignoreCase: false);
        using var directory = new TempDirectory();

        string withRelativeSegment = Path.Combine(directory.FullPath, "sub", "..");
        Assert.AreEqual(directory.FullPath, comparer.Normalize(withRelativeSegment));
    }

    [TestMethod]
    public void RootPathKeepsItsTrailingSeparator()
    {
        var comparer = new PathComparer(ignoreCase: true);

        Assert.AreEqual(@"C:\", comparer.Normalize(@"C:\"));
        Assert.AreEqual(@"C:\x", comparer.Normalize(@"C:\x\"));
    }

    [TestMethod]
    public void ForCurrentPlatformMatchesHostRule()
    {
        PathComparer comparer = PathComparer.ForCurrentPlatform();

        Assert.AreEqual(
            OperatingSystem.IsWindows(),
            comparer.Equals(@"C:\Same", @"c:\same"));
    }
}

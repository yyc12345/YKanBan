using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace YKanBan.Tests;

/// <summary>
/// Smoke tests verifying that the test project is correctly wired to the
/// application project and that the test runner can discover and execute tests.
/// </summary>
[TestClass]
public class ProjectReferenceTests {
    /// <summary>
    /// Verifies that a type defined in the application assembly can be
    /// referenced from the test project.
    /// </summary>
    [TestMethod]
    public void ApplicationAssemblyIsReferenceable() {
        Assert.IsNotNull(typeof(App));
    }
}

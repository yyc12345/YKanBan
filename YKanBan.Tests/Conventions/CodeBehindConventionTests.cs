using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace YKanBan.Tests.Conventions;

/// <summary>
/// Source-level guards for code-behind conventions (plan §8.1): inside control,
/// window and application code-behind a bare <c>Resources.Key</c> resolves to
/// the <c>StyledElement.Resources</c> instance property, so the generated class
/// must be written as <c>YKanBan.Resources</c> (or through a using alias).
/// </summary>
[TestClass]
public partial class CodeBehindConventionTests
{
    /// <summary>A <c>Resources.</c> member access not preceded by a qualifier or identifier character.</summary>
    [GeneratedRegex(@"(?<![\w.])Resources\.")]
    private static partial Regex BareResourcesAccess();

    /// <summary>
    /// Locates the application project's source folder from this file's compile-time path.
    /// </summary>
    /// <param name="thisFile">Filled in by the compiler.</param>
    /// <returns>The YKanBan project folder.</returns>
    private static string ApplicationSourceFolder([CallerFilePath] string thisFile = "")
    {
        DirectoryInfo? directory = new FileInfo(thisFile).Directory;
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "YKanBan", "YKanBan.csproj")))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, "Repository root not found from the test source path.");
        return Path.Combine(directory.FullName, "YKanBan");
    }

    [TestMethod]
    public void CodeBehindNeverUsesBareResourcesClassName()
    {
        string root = ApplicationSourceFolder();
        string[] files = Directory.GetFiles(root, "*.axaml.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .ToArray();
        Assert.IsTrue(files.Length > 0, "No code-behind files found.");

        var offenders = new List<string>();
        foreach (string file in files)
        {
            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++)
            {
                if (BareResourcesAccess().IsMatch(lines[index]))
                {
                    offenders.Add($"{Path.GetRelativePath(root, file)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        Assert.AreEqual(0, offenders.Count, "Use YKanBan.Resources in code-behind:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }
}

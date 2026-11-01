namespace NINAAllskySafetyMonitor.Tests.Traceability;

/// <summary>Locates the repository root from the test output folder.</summary>
public static class RepositoryPaths
{
    private const string SolutionFileName = "NINAAllskySafetyMonitor.slnx";

    /// <summary>The folder containing the solution file.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>docs/requirements/requirements.md.</summary>
    public static string RequirementsFile => Path.Combine(Root, "docs", "requirements", "requirements.md");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException($"Could not find {SolutionFileName} above {AppContext.BaseDirectory}.");
    }
}

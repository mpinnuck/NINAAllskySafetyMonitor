using Xunit;
using Xunit.Abstractions;

namespace NINAAllskySafetyMonitor.Tests.Traceability;

/// <summary>
/// Requirement coverage: every requirement must be implemented by a tagged class and verified by a tagged test,
/// and no tag may name an unknown requirement.
/// </summary>
public sealed class RequirementCoverageTests
{
    private const string PartialImplementation =
        "Enabled once every package is implemented; see CoverageReport for current gaps.";

    private readonly ITestOutputHelper output;
    private readonly IReadOnlyList<RequirementInfo> requirements;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> implemented;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> tested;

    public RequirementCoverageTests(ITestOutputHelper output)
    {
        this.output = output;
        requirements = RequirementsParser.Parse(RepositoryPaths.RequirementsFile);
        implemented = MergeScans(
            RequirementTagScanner.Scan(Path.Combine(RepositoryPaths.Root, "src")),
            RequirementTagScanner.Scan(Path.Combine(RepositoryPaths.Root, "tools")));
        tested = RequirementTagScanner.Scan(Path.Combine(RepositoryPaths.Root, "tests"));
    }

    [Fact]
    public void RequirementsFileHasRequirements()
    {
        Assert.NotEmpty(requirements);
        Assert.Equal(requirements.Count, requirements.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public void NoUnknownRequirementIds()
    {
        var known = requirements.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var unknown = implemented.Keys.Concat(tested.Keys)
            .Where(id => !known.Contains(id))
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.True(unknown.Count == 0, "Tags name unknown requirements: " + string.Join(", ", unknown));
    }

    [Fact(Skip = PartialImplementation)]
    public void EveryRequirementHasImplementation()
    {
        var missing = Missing(implemented);
        Assert.True(missing.Count == 0, "Requirements with no implementing class: " + string.Join(", ", missing));
    }

    [Fact(Skip = PartialImplementation)]
    public void EveryRequirementHasTest()
    {
        var missing = Missing(tested);
        Assert.True(missing.Count == 0, "Requirements with no test: " + string.Join(", ", missing));
    }

    [Fact]
    public void CoverageReport()
    {
        output.WriteLine($"Requirements: {requirements.Count}");
        output.WriteLine($"Implemented:  {requirements.Count - Missing(implemented).Count}");
        output.WriteLine($"Tested:       {requirements.Count - Missing(tested).Count}");
        output.WriteLine("Not implemented: " + string.Join(", ", Missing(implemented)));
        output.WriteLine("Not tested:      " + string.Join(", ", Missing(tested)));
    }

    private List<string> Missing(IReadOnlyDictionary<string, IReadOnlyList<string>> tags) =>
        requirements.Select(r => r.Id).Where(id => !tags.ContainsKey(id)).ToList();

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> MergeScans(
        IReadOnlyDictionary<string, IReadOnlyList<string>> a,
        IReadOnlyDictionary<string, IReadOnlyList<string>> b)
    {
        var merged = a.ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), StringComparer.Ordinal);
        foreach (var (id, files) in b)
        {
            if (merged.TryGetValue(id, out List<string>? list)) list.AddRange(files);
            else merged[id] = files.ToList();
        }

        return merged.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal);
    }
}

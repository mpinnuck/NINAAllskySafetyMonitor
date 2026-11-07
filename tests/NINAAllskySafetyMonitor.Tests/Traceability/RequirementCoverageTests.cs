using Xunit;
using Xunit.Abstractions;

namespace NINAAllskySafetyMonitor.Tests.Traceability;

/// <summary>
/// Requirement coverage: every requirement must be implemented by a tagged class and verified by a tagged test,
/// and no tag may name an unknown requirement.
/// </summary>
public sealed class RequirementCoverageTests
{
    private static readonly IReadOnlyDictionary<string, string> ImplementationGaps =
        new Dictionary<string, string>
        {
            ["REQ-29"] = "The low-count probability rule is pending recovery; existing star-test code only covers the standard fraction rule.",
        };

    private static readonly IReadOnlyDictionary<string, string> ManualVerification =
        new Dictionary<string, string>
        {
            ["REQ-21"] = "Calibration overlay requires visual verification in the NINA UI.",
            ["REQ-45"] = "Status view requires visual verification in the NINA UI.",
            ["REQ-47"] = "Calibration/settings views require visual verification in the NINA UI.",
            ["REQ-48"] = "Release packaging requires the AstroPC's installed NINA package and runtime smoke test.",
            ["REQ-59"] = "Enlarged viewer and cursor mapping require visual verification in the NINA UI.",
        };

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

    [Fact]
    public void EveryRequirementHasImplementation()
    {
        var missing = Missing(implemented, ImplementationGaps.Keys);
        Assert.True(missing.Count == 0, "Requirements with no implementing class: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryRequirementHasTest()
    {
        var missing = Missing(tested, ManualVerification.Keys.Concat(ImplementationGaps.Keys));
        Assert.True(missing.Count == 0, "Requirements with no test: " + string.Join(", ", missing));
    }

    [Fact]
    public void CoverageReport()
    {
        output.WriteLine($"Requirements: {requirements.Count}");
        output.WriteLine($"Implemented:  {requirements.Count - Missing(implemented, ImplementationGaps.Keys).Count - ImplementationGaps.Count}");
        output.WriteLine($"Tested:       {requirements.Count - Missing(tested, ManualVerification.Keys.Concat(ImplementationGaps.Keys)).Count - ManualVerification.Count - ImplementationGaps.Count}");
        output.WriteLine("Not implemented: " + string.Join(", ", Missing(implemented, ImplementationGaps.Keys)));
        output.WriteLine("Not tested:      " + string.Join(", ", Missing(tested, ManualVerification.Keys.Concat(ImplementationGaps.Keys))));
        output.WriteLine("Known implementation gaps: " + string.Join("; ", ImplementationGaps.Select(kv => $"{kv.Key}: {kv.Value}")));
        output.WriteLine("Manual verification: " + string.Join("; ", ManualVerification.Select(kv => $"{kv.Key}: {kv.Value}")));
    }

    private List<string> Missing(IReadOnlyDictionary<string, IReadOnlyList<string>> tags, IEnumerable<string>? exempt = null)
    {
        var exemptions = (exempt ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        return requirements.Select(r => r.Id)
            .Where(id => !tags.ContainsKey(id) && !exemptions.Contains(id))
            .ToList();
    }

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

using System.Text.RegularExpressions;

namespace NINAAllskySafetyMonitor.Tests.Traceability;

/// <summary>Reads the requirement list from requirements.md (lines of the form "- **REQ-nn** text").</summary>
public static class RequirementsParser
{
    private static readonly Regex RequirementLine =
        new(@"^- \*\*(?<id>REQ-\d+)\*\*\s*(?<text>.*)$", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Parses all requirements in the markdown file, in document order.</summary>
    public static IReadOnlyList<RequirementInfo> Parse(string markdownPath)
    {
        string markdown = File.ReadAllText(markdownPath);
        return RequirementLine.Matches(markdown)
            .Select(m => new RequirementInfo(m.Groups["id"].Value, m.Groups["text"].Value.Replace("**", string.Empty).Trim()))
            .ToList();
    }
}

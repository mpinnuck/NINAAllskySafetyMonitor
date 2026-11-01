using System.Text.RegularExpressions;

namespace NINAAllskySafetyMonitor.Tests.Traceability;

/// <summary>
/// Finds [Requirement("REQ-nn", ...)] tags in C# source files. Scanning source rather than
/// assemblies lets one test cover every project, including the Windows-only plugin.
/// </summary>
public static class RequirementTagScanner
{
    private static readonly Regex Attribute =
        new(@"\[Requirement\((?<args>[^)]*)\)\]", RegexOptions.Compiled);

    private static readonly Regex Id = new(@"REQ-\d+", RegexOptions.Compiled);

    /// <summary>Maps each requirement ID to the source files that tag it, under <paramref name="folder"/>.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Scan(string folder)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file)) continue;

            string source = File.ReadAllText(file);
            foreach (Match attribute in Attribute.Matches(source))
            {
                foreach (Match id in Id.Matches(attribute.Groups["args"].Value))
                {
                    if (!map.TryGetValue(id.Value, out List<string>? files))
                    {
                        files = new List<string>();
                        map[id.Value] = files;
                    }

                    string name = Path.GetRelativePath(folder, file);
                    if (!files.Contains(name)) files.Add(name);
                }
            }
        }

        return map.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal);
    }

    private static bool IsBuildOutput(string file)
    {
        string sep = Path.DirectorySeparatorChar.ToString();
        return file.Contains(sep + "bin" + sep) || file.Contains(sep + "obj" + sep);
    }
}

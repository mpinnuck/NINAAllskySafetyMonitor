using System.Text.RegularExpressions;

namespace NINAAllskySafetyMonitor.Tests.Traceability;

/// <summary>
/// Finds requirement tags in source files: [Requirement("REQ-nn", ...)] in C# and @requirement("REQ-nn", ...)
/// in Python (the review tool). Scanning source rather than assemblies lets one test cover every project,
/// including the Windows-only plugin and the Python review tool.
/// </summary>
public static class RequirementTagScanner
{
    private static readonly Regex Attribute =
        new(@"Requirement\s*\((?<args>[^)]*)\)", RegexOptions.Compiled);

    private static readonly Regex Decorator =
        new(@"^\s*@requirement\((?<args>[^)]*)\)", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex Id = new(@"REQ-\d+", RegexOptions.Compiled);

    /// <summary>Maps each requirement ID to the source files that tag it, under <paramref name="folder"/>.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Scan(string folder)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
        {
            Regex? tag = Path.GetExtension(file) switch
            {
                ".cs" => Attribute,
                ".py" => Decorator,
                _ => null,
            };
            if (tag is null || IsBuildOutput(file)) continue;

            string source = File.ReadAllText(file);
            foreach (Match attribute in tag.Matches(source))
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
        return file.Contains(sep + "bin" + sep) || file.Contains(sep + "obj" + sep)
            || file.Contains(sep + "__pycache__" + sep) || file.Contains(sep + ".venv" + sep) || file.Contains(sep + "venv" + sep);
    }
}

using NINAAllskySafetyMonitor.Core.Traceability;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Traceability;

[Requirement("REQ-62")]
public sealed class RequirementTagScannerTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("NASM-Scan-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void FindsCSharpAttributesAndPythonDecorators()
    {
        File.WriteAllText(Path.Combine(folder, "A.cs"), "[Requirement(\"REQ-01\", \"REQ-02\")]\npublic class A {}");
        File.WriteAllText(Path.Combine(folder, "Combined.cs"), "[Fact, Requirement(\"REQ-13\", \"REQ-43\")]\npublic void Combined() {}");
        File.WriteAllText(Path.Combine(folder, "b.py"), "from x import requirement\n\n\n@requirement(\"REQ-62\")\nclass B:\n    pass\n");
        File.WriteAllText(Path.Combine(folder, "c.py"), "# @requirement(\"REQ-99\") in a comment is not a tag\nx = 1\n");
        Directory.CreateDirectory(Path.Combine(folder, "__pycache__"));
        File.WriteAllText(Path.Combine(folder, "__pycache__", "d.py"), "@requirement(\"REQ-98\")\nclass D: pass\n");

        IReadOnlyDictionary<string, IReadOnlyList<string>> map = RequirementTagScanner.Scan(folder);

        Assert.Equal(new[] { "REQ-01", "REQ-02", "REQ-13", "REQ-43", "REQ-62" }, map.Keys.Order().ToArray());
        Assert.Equal(new[] { "b.py" }, map["REQ-62"]);
        Assert.Equal(new[] { "Combined.cs" }, map["REQ-13"]);
    }
}

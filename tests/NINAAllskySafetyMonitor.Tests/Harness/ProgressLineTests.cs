using NINAAllskySafetyMonitor.Core.Traceability;
using NINAAllskySafetyMonitor.Harness;
using Xunit;

namespace NINAAllskySafetyMonitor.Tests.Harness;

[Requirement("REQ-42")]
public sealed class ProgressLineTests
{
    [Fact]
    public void OnTheConsoleEachUpdateOverwritesTheLastAndMessagesGoAbove()
    {
        var output = new StringWriter();
        var progress = new ProgressLine(output, inPlace: true);

        progress.Update("10/100 frames", force: true);
        progress.Update("20/100", force: true);
        progress.Message("Clear-sky reference captured");
        progress.Update("30/100 frames", force: true);
        progress.Finish();

        Assert.Equal(
            "\r10/100 frames" + "\r20/100       " + "\r" + new string(' ', 6) + "\r" + "Clear-sky reference captured" + output.NewLine +
            "\r30/100 frames" + "\r" + new string(' ', 13) + "\r",
            output.ToString());
    }

    [Fact]
    public void UpdatesInQuickSuccessionAreShownOnce()
    {
        var output = new StringWriter();
        var progress = new ProgressLine(output, inPlace: true);

        progress.Update("1");
        progress.Update("2");
        progress.Update("3", force: true);

        Assert.Equal("\r1\r3", output.ToString());
    }

    [Fact]
    public void RedirectedOutputGetsOneLinePerUpdate()
    {
        var output = new StringWriter();
        var progress = new ProgressLine(output, inPlace: false);

        progress.Update("a", force: true);
        progress.Message("note");
        progress.Update("b", force: true);
        progress.Finish();

        Assert.Equal($"a{output.NewLine}note{output.NewLine}b{output.NewLine}", output.ToString());
    }
}

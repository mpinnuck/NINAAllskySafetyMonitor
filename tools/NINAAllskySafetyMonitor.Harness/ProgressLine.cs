using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>
/// Shows replay progress on one console line, overwritten in place. Other messages are written on their own
/// line above it. When the output is redirected to a file, each update is written as a new line instead,
/// at most every <see cref="RedirectedInterval"/>.
/// </summary>
[Requirement("REQ-42")]
public sealed class ProgressLine
{
    private static readonly TimeSpan ConsoleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RedirectedInterval = TimeSpan.FromSeconds(30);

    private readonly object sync = new();
    private readonly bool inPlace;
    private readonly TextWriter output;
    private DateTime lastShown = DateTime.MinValue;
    private int shownLength;

    public ProgressLine()
        : this(Console.Out, !Console.IsOutputRedirected)
    {
    }

    public ProgressLine(TextWriter output, bool inPlace)
    {
        this.output = output ?? throw new ArgumentNullException(nameof(output));
        this.inPlace = inPlace;
    }

    /// <summary>Shows the progress text, unless it was shown very recently and <paramref name="force"/> is false.</summary>
    public void Update(string text, bool force = false)
    {
        lock (sync)
        {
            DateTime now = DateTime.UtcNow;
            if (!force && now - lastShown < (inPlace ? ConsoleInterval : RedirectedInterval)) return;
            lastShown = now;
            if (inPlace)
            {
                output.Write('\r' + text.PadRight(shownLength));
                shownLength = text.Length;
            }
            else
            {
                output.WriteLine(text);
            }

            output.Flush();
        }
    }

    /// <summary>Writes a message on its own line, keeping the progress line below it.</summary>
    public void Message(string text)
    {
        lock (sync)
        {
            EndLine();
            output.WriteLine(text);
            output.Flush();
        }
    }

    /// <summary>Moves past the progress line, so later output starts on a new line.</summary>
    public void Finish()
    {
        lock (sync)
        {
            EndLine();
            output.Flush();
        }
    }

    private void EndLine()
    {
        if (!inPlace || shownLength == 0) return;
        output.Write('\r' + new string(' ', shownLength) + '\r');
        shownLength = 0;
        lastShown = DateTime.MinValue;
    }
}

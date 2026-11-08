using System.Diagnostics;
using System.Globalization;
using NINAAllskySafetyMonitor.Core.Settings;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Harness;

/// <summary>
/// Offline replay (REQ-42, REQ-61). Runs on macOS and Windows:
/// <code>
/// dotnet run --project tools/NINAAllskySafetyMonitor.Harness -- replay --frames ~/AllskyFrames
///     --settings settings.json --out ~/Replay/run1 [--nights 20261019-20261101] [--step 60]
///     [--dec -45:45:10] [--ra-step 1] [--target "05:35,-05:23"] [--near 0.1]
///     [--use-settings-reference] [--delay 0] [--zone Australia/Sydney]
/// </code>
/// Writes results.csv, review.xlsx and review/ (marked-up copies) to the output folder. Ctrl+C stops after
/// the current frame and still writes the review workbook.
/// </summary>
[Requirement("REQ-42", "REQ-61")]
internal static class Program
{
    private const string Usage = """
        NINAAllskySafetyMonitor.Harness replay --frames <folder> --settings <settings.json> --out <folder>
            [--nights yyyyMMdd-yyyyMMdd] [--step 60] [--dec -45:45:10] [--ra-step 1]
            [--target "RA,Dec"] [--target "name=RA,Dec"] [--near 0.1]
            [--use-settings-reference] [--save-settings <file>] [--delay 0] [--zone Australia/Sydney]

          --frames    Allsky images folder (night folders yyyyMMdd containing *yyyyMMddHHmmss.jpg), or one night folder
          --settings  the plugin's settings.json (lens, working area, thresholds); copy it and edit the
                      thresholds to try other values
          --out       output folder: results.csv, review.xlsx, review/
          --nights    first-last night to replay (default: all)
          --step      seconds between frames used (default 60, like the plugin's cycle)
          --dec       declinations from:to:step in degrees (default -45:45:10)
          --ra-step   hours between right ascensions (default 1)
          --target    fixed target followed through the night, as RA,Dec (repeatable). RA is decimal
                      hours or hh:mm[:ss]; Dec is decimal degrees or ±dd:mm[:ss]. A name may be
                      supplied as name=RA,Dec. When any --target is given, the RA/Dec grid is not used.
                      Examples: --target "05:35,-05:23" --target "M42=05:35,-05:23"
          --near      fraction of a limit counted as near it (default 0.1)
          --use-settings-reference  use the clear-sky reference in the settings file instead of capturing one
          --save-settings  write a copy of the settings with the captured clear-sky reference after replay
          --delay     pause after each frame, ms (default 0)
          --zone      time zone of the times in the frame names (default: this computer's)
        """;

    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return 0;
        }

        if (args[0] != "replay")
        {
            Console.Error.WriteLine($"Unknown command '{args[0]}'.\n\n{Usage}");
            return 2;
        }

        ReplayOptions options;
        try
        {
            options = Parse(args.Skip(1).ToArray());
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine($"{e.Message}\n\n{Usage}");
            return 2;
        }

        try
        {
            return Replay(options);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return 1;
        }
    }

    internal static ReplayOptions Parse(string[] args)
    {
        var o = new ReplayOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
            o = args[i] switch
            {
                "--frames" => o with { FramesFolder = Expand(Next()) },
                "--settings" => o with { SettingsFile = Expand(Next()) },
                "--out" => o with { OutputFolder = Expand(Next()) },
                "--nights" => Nights(o, Next()),
                "--step" => o with { StepS = int.Parse(Next(), CultureInfo.InvariantCulture) },
                "--dec" => Dec(o, Next()),
                "--ra-step" => o with { RaStepH = double.Parse(Next(), CultureInfo.InvariantCulture) },
                "--target" => Target(o, Next()),
                "--near" => o with { NearLimit = double.Parse(Next(), CultureInfo.InvariantCulture) },
                "--use-settings-reference" => o with { UseSettingsReference = true },
                "--save-settings" => o with { SaveSettingsFile = Expand(Next()) },
                "--delay" => o with { DelayMs = int.Parse(Next(), CultureInfo.InvariantCulture) },
                "--zone" => o with { Zone = TimeZoneInfo.FindSystemTimeZoneById(Next()) },
                _ => throw new ArgumentException($"Unknown option {args[i]}."),
            };
        }

        if (string.IsNullOrEmpty(o.FramesFolder) || string.IsNullOrEmpty(o.SettingsFile) || string.IsNullOrEmpty(o.OutputFolder))
        {
            throw new ArgumentException("--frames, --settings and --out are required.");
        }

        if (o.StepS < 1 || o.RaStepH <= 0 || o.DecStepDeg <= 0 || o.NearLimit < 0) throw new ArgumentException("Step values must be positive.");
        return o;
    }

    internal static int Replay(ReplayOptions o)
    {
        if (!File.Exists(o.SettingsFile)) throw new IOException($"Settings file not found: {o.SettingsFile}");
        MonitorSettings settings = new SettingsStore(o.SettingsFile).Load();
        Directory.CreateDirectory(o.OutputFolder);

        IReadOnlyList<SavedFrame> frames = new FrameCatalog().List(o);
        if (frames.Count == 0)
        {
            throw new InvalidDataException($"No Allsky frames found under '{o.FramesFolder}'. Expected yyyyMMdd night folders and JPEG filenames containing a yyyyMMddHHmmss timestamp.");
        }

        var grid = new TargetGrid(o);
        var analyser = new FrameAnalyser(settings, o, new SkiaFrameDecoder(), grid);
        var selector = new ReviewSelector(o.NearLimit);
        var annotator = new AnnotatedFrameWriter(analyser.Lens, analyser.WorkingArea, o.OutputFolder, o.Zone);
        var workbook = new ReviewWorkbookWriter(o.Zone);
        Console.WriteLine($"{frames.Count} frames in {frames.Select(f => f.Night).Distinct().Count()} nights; {grid.Targets.Count} targets. Output: {o.OutputFolder}");

        bool[] isNight = frames.Select(analyser.IsNight).ToArray();
        int nightTotal = isNight.Count(n => n);
        Console.WriteLine($"{nightTotal} of them with the Sun below {settings.Window.SunAltitudeLimitDeg:0.#}° are judged; the rest are skipped.");

        var progress = new ProgressLine();
        bool stop = false;
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop = true;
            progress.Message("Stopping after this frame…");
        };

        var nightClock = new Stopwatch();
        int night = 0, skyless = 0, judged = 0, failed = 0;
        string referenceText = o.UseSettingsReference
            ? settings.Calibration.ClearSkyReference is { } r ? $"From the settings file ({r.CapturedUtc:yyyy-MM-dd HH:mm} UTC)" : "None in the settings file"
            : "Not captured (no clear moonless frame)";
        using (var csv = new ResultsCsvWriter(Path.Combine(o.OutputFolder, "results.csv"), o.Zone))
        {
            for (int i = 0; i < frames.Count && !stop; i++)
            {
                SavedFrame f = frames[i];
                if (isNight[i]) nightClock.Start();
                FrameResult result;
                try
                {
                    result = analyser.Analyse(f);
                }
                catch (Exception e) when (e is IOException or InvalidDataException)
                {
                    failed++;
                    progress.Message($"Skipped {Path.GetFileName(f.Path)}: {e.Message}");
                    continue;
                }
                finally
                {
                    nightClock.Stop();
                    if (isNight[i]) night++;
                }

                if (result.SkyFrame is { Passed: false }) skyless++;
                judged += result.Targets.Count;
                if (result.ReferenceCaptured)
                {
                    referenceText = $"Captured from {Path.GetFileName(f.Path)}";
                    progress.Message($"Clear-sky reference captured from {Path.GetFileName(f.Path)}");
                }

                csv.Append(result, analyser.Reference is not null);
                foreach ((TargetResult t, string why) in selector.Select(result, settings.Cloud))
                {
                    workbook.Add(f, t, why, annotator.Write(f, t, why));
                }

                progress.Update(ProgressText(f, o.Zone, i + 1, frames.Count, night, nightTotal, nightClock.Elapsed, workbook.Count), i == frames.Count - 1);

                if (o.DelayMs > 0) Thread.Sleep(o.DelayMs);
            }

            progress.Finish();
            Console.WriteLine($"Night frames {night}, not sky frames {skyless}, unreadable {failed}; {judged} target results in results.csv ({csv.Rows} rows).");
        }

        workbook.Save(Path.Combine(o.OutputFolder, "review.xlsx"), settings, o, referenceText);
        Console.WriteLine($"{workbook.Count} frames to review in review.xlsx. Reference: {referenceText}.");

        if (!string.IsNullOrEmpty(o.SaveSettingsFile))
        {
            if (analyser.Reference is null)
            {
                Console.Error.WriteLine("No clear-sky reference was captured; --save-settings was not written.");
                return 1;
            }

            MonitorSettings saved = SettingsStore.Clone(settings);
            saved.Calibration.ClearSkyReference = analyser.Reference;
            new SettingsStore(o.SaveSettingsFile).Save(saved);
            Console.WriteLine($"Saved settings with the captured reference to {o.SaveSettingsFile}.");
        }

        return 0;
    }

    private static string ProgressText(SavedFrame f, TimeZoneInfo zone, int done, int total, int night, int nightTotal, TimeSpan nightTime, int toReview)
    {
        string local = TimeZoneInfo.ConvertTimeFromUtc(f.CaptureUtc, zone).ToString("HH:mm", CultureInfo.InvariantCulture);
        if (night == 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{done}/{total} frames · {f.Night:yyyy-MM-dd} {local} · skipping daylight · {toReview} to review");
        }

        double rate = nightTime.TotalSeconds / night;
        TimeSpan left = TimeSpan.FromSeconds(rate * (nightTotal - night));
        return string.Create(CultureInfo.InvariantCulture,
            $"{done}/{total} frames · {f.Night:yyyy-MM-dd} {local} · judged {night}/{nightTotal} · {rate:0.0} s/frame · {(int)left.TotalHours}:{left.Minutes:00} left · {toReview} to review");
    }

    private static ReplayOptions Target(ReplayOptions o, string v)
    {
        ReplayTarget target = ParseTarget(v, o.Targets.Count + 1);
        return o with { Targets = o.Targets.Append(target).ToArray() };
    }

    internal static ReplayTarget ParseTarget(string value, int index)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("--target needs RA,Dec.");

        string text = value.Trim();
        string name = string.Empty;
        int equals = text.IndexOf('=');
        if (equals >= 0)
        {
            name = text[..equals].Trim();
            text = text[(equals + 1)..].Trim();
            if (name.Length == 0) throw new ArgumentException("--target name must not be empty.");
        }

        string[] parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) throw new ArgumentException("--target needs RA,Dec (for example \"05:35,-05:23\").");

        double ra = ParseRa(parts[0]);
        double dec = ParseDec(parts[1]);
        string id = string.Create(CultureInfo.InvariantCulture,
            $"{(name.Length == 0 ? $"Target {index:00}" : name)} RA{ra:0.###} Dec{dec:+0.###;-0.###;+0.###}");
        return new ReplayTarget(id, ra, dec);
    }

    private static double ParseRa(string value)
    {
        string[] parts = value.Split(':');
        if (parts.Length is < 1 or > 3) throw new ArgumentException($"Invalid RA '{value}'.");
        double hours = Number(parts[0]);
        double minutes = parts.Length > 1 ? Number(parts[1]) : 0;
        double seconds = parts.Length > 2 ? Number(parts[2]) : 0;
        if (hours < 0 || minutes < 0 || minutes >= 60 || seconds < 0 || seconds >= 60)
            throw new ArgumentException($"Invalid RA '{value}'.");
        double total = hours + (minutes / 60) + (seconds / 3600);
        if (total >= 24) throw new ArgumentException($"RA must be below 24 hours: '{value}'.");
        return total;
    }

    private static double ParseDec(string value)
    {
        string trimmed = value.Trim();
        bool negative = trimmed.StartsWith("-", StringComparison.Ordinal);
        string[] parts = trimmed.TrimStart('+', '-').Split(':');
        if (parts.Length is < 1 or > 3) throw new ArgumentException($"Invalid Dec '{value}'.");
        double degrees = Number(parts[0]);
        double minutes = parts.Length > 1 ? Number(parts[1]) : 0;
        double seconds = parts.Length > 2 ? Number(parts[2]) : 0;
        if (minutes < 0 || minutes >= 60 || seconds < 0 || seconds >= 60)
            throw new ArgumentException($"Invalid Dec '{value}'.");
        double total = degrees + (minutes / 60) + (seconds / 3600);
        if (total > 90) throw new ArgumentException($"Dec must be between -90 and +90: '{value}'.");
        return negative ? -total : total;
    }

    private static double Number(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n)
            ? n
            : throw new ArgumentException($"'{value}' is not a number.");

    private static ReplayOptions Nights(ReplayOptions o, string v)
    {
        string[] p = v.Split('-');
        DateOnly first = DateOnly.ParseExact(p[0], "yyyyMMdd", CultureInfo.InvariantCulture);
        DateOnly last = p.Length > 1 ? DateOnly.ParseExact(p[1], "yyyyMMdd", CultureInfo.InvariantCulture) : first;
        return o with { FirstNight = first, LastNight = last };
    }

    private static ReplayOptions Dec(ReplayOptions o, string v)
    {
        double[] p = v.Split(':').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        if (p.Length != 3) throw new ArgumentException("--dec needs from:to:step, for example -45:45:10.");
        return o with { DecFromDeg = p[0], DecToDeg = p[1], DecStepDeg = p[2] };
    }

    private static string Expand(string path) =>
        path.StartsWith('~') ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.TrimStart('~').TrimStart('/', '\\')) : path;
}

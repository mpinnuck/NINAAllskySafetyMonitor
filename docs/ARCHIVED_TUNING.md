# Tuning from the Allsky archive with simulated mounts

The archived Allsky frames contain the same pixels as a live frame. They are sufficient to tune
the star test, the brightness test and the thresholds before the first clear night. What the archive
does not contain automatically is metadata: the mount target, the manual clear/cloud label, the
weather, and a persisted clear-sky reference. The harness fills in the mount target with a simulator;
the other items need a small amount of manual work.

## 1. Nominate a mount target

A fixed J2000 RA/Dec is followed through every archived frame, like a mount tracking that target:

```bash
dotnet run --project tools/NINAAllskySafetyMonitor.Harness -- replay \
  --frames "/path/to/AllskyFrames" \
  --settings "/path/to/settings.json" \
  --out "/path/to/Replay/M42" \
  --zone Australia/Sydney --step 300 \
  --target "M42=05:35,-05:23" \
  --save-settings "/path/to/settings-tuned.json"
```

- RA: decimal hours or `hh:mm[:ss]` (0 ≤ RA < 24).
- Dec: decimal degrees or `±dd:mm[:ss]` (−90 ≤ Dec ≤ +90).
- Name is optional; `05:35,-05:23` gives an automatic `Target 01 …` id.
- Repeat `--target` for several mounts. This replaces the grid.
- At each frame the ROI is rebuilt at the target's current alt/az, so the run covers rise, transit
  and set, and the target is skipped while below the horizon or outside the working area.

## 2. Or test a grid of ROIs

For broad threshold tuning across the working area, use the existing grid instead of `--target`:

```bash
dotnet run --project tools/NINAAllskySafetyMonitor.Harness -- replay \
  --frames "/path/to/AllskyFrames" \
  --settings "/path/to/settings.json" \
  --out "/path/to/Replay/dec50" \
  --zone Australia/Sydney --step 300 \
  --dec -50:50:10 --ra-step 1
```

This generates RA every hour and Dec from −50° to +50° every 10°. Only targets whose ROI is
inside the working area are judged at each frame, so the actual set changes through the night.
Use `--nights yyyyMMdd-yyyyMMdd` to limit the period.

## 3. Workflow

1. **Verify the lens calibration** against the archive resolution. The harness fails without a lens.
2. **Run a single clear archived night** at `--step 300` first. Check `results.csv`:
   - clear frames should not be predominantly Cloud;
   - expected/found star counts should be plausible;
   - the first clear moonless frame should print `Clear-sky reference captured`.
3. **Sample labelled conditions.** Use the review workbook and the images to label frames Clear /
   Thin / Thick / Unsure. A few dozen frames across clear, thin and thick cloud are enough to start;
   do not tune on one frame.
4. **Tune the star test first**: limiting magnitude, minimum expected stars, minimum fraction.
   Fix calibration/ROI before changing thresholds.
5. **Tune brightness second**, only when a reference was captured from a clear moonless frame.
6. **Re-run with a settings copy** and compare results.csv/review.xlsx. Keep a held-out night.
7. **Persist the settings** with `--save-settings` once the false-cloud/missed-cloud trade-off is acceptable.

## 4. Reference and ROI caveats

- `--save-settings` writes a copy of the settings with the clear-sky reference captured during the
  replay. Use a new path, not the live settings file. If no clear moonless frame was found, the
  option fails and no file is written.
- The saved file contains the same lens, working area and thresholds as the input, plus the captured
  reference. Copy it into the plugin's settings location before the first clear night; the archived
  reference is only valid for the same camera/optics and image size. The first live night is then an
  integration check (image fetch, timestamps, device/weather, settings loaded), not a threshold-tuning
  session.
- A target nominated by `--target` uses the same target coordinates and ROI geometry as the plugin.
  Mount tracking error, meridian flips and cable wrap do not change the cloud-detection settings:
  they are pointing/acquisition concerns, not image-processing concerns. If a local obstruction
  covers the ROI, that frame is not a valid sky sample; skip it rather than tune on it.
- The grid is not the actual mount target; it is a survey of ROIs. Use `--target` when you want the
  exact ROI, and the grid when you want broad threshold coverage.
- Weather is not replayed. Keep weather separate from cloud tuning.
- The Sun-altitude window, not the wall clock, decides which frames are night frames. A target below
  the horizon is skipped at that time.

## 5. Suggested first pass

```bash
# One representative clear night, one target, 5-minute sampling
dotnet run --project tools/NINAAllskySafetyMonitor.Harness -- replay \
  --frames "/path/to/AllskyFrames" --settings "/path/to/settings.json" \
  --out "/path/to/Replay/test" --zone Australia/Sydney --step 300 \
  --nights 20261020-20261020 --target "M42=05:35,-05:23"
```

Then review `results.csv` and `review.xlsx`, label a balanced sample, and only then adjust one or two
thresholds in a copy of the settings file.

# Slice 7 verification: offline replay harness and review tool

Requirements: REQ-42, REQ-61, REQ-62 (also REQ-29, REQ-30, REQ-31, REQ-60). Requirements v5.10, design v1.12.

The harness re-runs the plugin's cloud detection over saved Allsky frames, on the Mac, without NINA.
- **Frames:** night folders `yyyyMMdd` of `allsky-yyyyMMddHHmmss.jpg`, or one night folder. One frame per 60 s is used (`--step`). Frames with the Sun above the plugin's limit are skipped.
- **Sky positions (REQ-42):** a grid of targets, Dec −45° to +45° every 10°, RA every 1 h (240). Each frame judges the targets whose ROI is at least half inside the working area, so each target is followed as it moves through the night.
- **Clear-sky reference:** captured from the replayed frames on the first clear, moonless frame, the same way as the plugin. `--use-settings-reference` uses the one in settings.json instead.
- **Results (REQ-61):**
  - `results.csv`: a row per frame and target, with metrics and verdict;
  - `review/yyyyMMdd/HHmmss-<target>.jpg`: a marked-up copy where a target's verdict changes, or a metric is within 10% of its limit (`--near`). It shows the working area (yellow dashed), the outline (blue), the ROI (green), the expected stars (green found, red missing) and a banner;
  - `review.xlsx`: a **Review** sheet with a row per marked-up copy, an **open** link to the image, a **My verdict** drop-down (Clear, Cloud, Unsure) and an **Agrees** column; a **Summary** sheet counting agreement; a **Settings** sheet recording the run.
- **Thresholds:** taken from the settings file. Copy it and edit the thresholds to try other values.

Record results in `verification.xlsx`, sheet **Slice 7**.

## A. Mac / B. AstroPC

Build and test on the Mac (all pass, two skipped), commit and push. Then pull, rebuild and run Test Explorer on the AstroPC; the harness builds there too, but is run on the Mac.

## C. Replay checks (Mac, Terminal, in the repo folder)

Preparation:
1. Copy the frames from the Pi:
   `rsync -av --include='*/' --include='allsky-????????19*.jpg' --include='allsky-????????2[0-3]*.jpg' --include='allsky-????????0[0-5]*.jpg' --exclude='*' pi@allsky.local:/home/pi/allsky/images/ ~/AllskyFrames/`
   (night frames only, 19:00–05:59; rsync can be stopped and restarted)
2. Copy the plugin's settings from the AstroPC, `%LOCALAPPDATA%\NINAAllskySafetyMonitor\settings.json`, to `~/AllskyFrames/settings.json`. The lens must be calibrated and the working area set. In the copy, set `Window.SunAltitudeLimitDeg` to −18 if it was raised for daylight tests, or every daytime frame is judged too.
3. Everything for tuning stays in `~/AllskyFrames`: night folders, `settings.json` and `Replay/` (the harness reads only the date-named folders).

| # | Step | Expected | REQ |
|---|---|---|---|
| 1 | `dotnet run --project tools/NINAAllskySafetyMonitor.Harness` | Usage text listing `replay` and its options | 42 |
| 2 | `dotnet run --project tools/NINAAllskySafetyMonitor.Harness -- replay --frames ~/AllskyFrames --settings ~/AllskyFrames/settings.json --out ~/AllskyFrames/Replay/run1 --nights <first night>-<first night>` | "n frames in 1 nights; m grid targets" and how many are judged; one progress line updated in place (frame, local time, judged so far, s/frame, time left); daylight frames skipped quickly; "Clear-sky reference captured from allsky-…jpg" if the night had a clear moonless frame; a final count of rows and frames to review. Rough time: about 5 s per judged frame, 40–50 min per night | 42 |
| 3 | Open `~/AllskyFrames/Replay/run1/results.csv` in Excel | One row per frame and target; night, times, file, Sun, target, ROI position and % inside, stars expected/found, star fraction, brightness, non-uniformity, Moon, verdict, summary, reference. Day frames absent; a notice image, if any, has a "Not a sky frame" row | 42, 60, 61 |
| 4 | Open `review.xlsx`, sheet **Review** | A row per marked-up copy, with **Picked because** ("Clear → Cloud", "star fraction near limit", …) | 61 |
| 5 | Click **open** on a few rows | The image opens: working area, outline, ROI, green/red stars and banner on the right target. Stars sit on real stars; ROI moves with the target between rows | 29, 61 |
| 6 | Pick **My verdict** for 10 or more rows (Clear / Cloud / Unsure) | **Agrees** shows Yes / No (blank for Unsure); **Summary** counts reviewed, agree, disagree and agreement % | 61 |
| 7 | Rows where the target was near the Moon | Fewer expected stars; Moon in the banner | 31 |
| 8 | Run the whole two weeks: as check 2 without `--nights`, `--out ~/AllskyFrames/Replay/all` | All nights replayed; Ctrl+C part-way stops after the current frame and still writes results.csv and review.xlsx | 42, 61 |
| 9 | `--step 300` on one night | About a fifth of the frames of check 2 | 42 |
| 10 | Copy settings.json, change **cloud below** from 70 to 60 % (`Cloud.MinStarFraction` 0.70 → 0.60), replay one night to `~/AllskyFrames/Replay/run60` | Fewer Cloud verdicts in results.csv than run1; the **Settings** sheet records the file used | 42 |
| 11 | `--use-settings-reference` on a night (settings with a captured reference) | Reference column "In use" from the first frame; no "captured" message | 30, 42 |

## D. Review tool (Mac, Terminal, in the repo folder)

Preparation: `pip3 install -r tools/review/requirements.txt` (Pillow, openpyxl, pytest). Tkinter comes with the python.org Python; with Homebrew's Python also `brew install python-tk`. Close `review.xlsx` in Excel before starting.

| # | Step | Expected | REQ |
|---|---|---|---|
| D1 | `python3 -m pytest tests/review` | All pass | 62 |
| D2 | `python3 tools/review/review.py ~/AllskyFrames/Replay/run1` | Window: the marked-up frame on the left, metrics on the right (night, time, target, monitor verdict in colour, stars, fraction, brightness, non-uniformity, picked because), "1 of n", the counts and the keys. Starts at the first frame without a verdict | 62 |
| D3 | Press **C**, **X**, **U** on three frames | Each verdict shows, the next frame appears straight away; counts update (reviewed, agree, disagree, false cloud, missed cloud) | 62 |
| D4 | **↑**, **↓**, mouse wheel, **N**, **Delete** | Previous / next (keys and wheel); next not reviewed; Delete clears my verdict on the current frame | 62 |
| D5 | **F** repeatedly, or the **Show** menu | Not reviewed yet, Monitor said Cloud, Monitor said Clear, Verdicts disagree, All frames; "n of m" changes with the filter | 62 |
| D6 | Click on the frame, click again | Zooms 3× at the point clicked; back to the whole frame | 62 |
| D7 | Open review.xlsx in Excel, press **C** in the tool; close Excel, press **X** | First: red message "review.xlsx is open in Excel…", the verdict kept; second: both saved | 62 |
| D8 | Close the tool, open review.xlsx in Excel | My verdict column has the verdicts, Agrees Yes/No, the drop-down still works, Summary counts match the tool's | 61, 62 |
| D9 | Close the tool on a frame part-way through with a filter chosen; run it again on the same folder | Opens on the same frame with the same filter (`.review-state.json` in the run folder); delete that file and it starts at the first frame without a verdict | 62 |
| D10 | Click a line in the table under the frame; then **↑**/**↓** with the table selected | That frame is shown; the selected line follows the frame; lines where the verdicts disagree are red; the Local time column shows the full date and time | 62 |
| D11 | Press **S**; use **↑/↓** and **←/→**, then hold **Shift** while nudging; navigate to another frame | Altitude and azimuth move by 1° (5° with Shift), altitude clamps at 0–90°, azimuth wraps around 0–360°, and each nudge previews on the clean source frame while navigating; the original workbook image is unchanged | 63 |
| D12 | Choose **Save**, then inspect settings.json and settings.json.bak | Only `Calibration.WorkingArea.CentreAltDeg`, `CentreAzDeg`, and `RadiusDeg` changed; the first backup matches the settings file before editing | 63 |

Unit tests behind this slice:
- Harness: `FrameCatalogTests`, `TargetGridTests`, `ReviewSelectorTests`, `ProgressLineTests`, `ReplayTests` (decoder, end-to-end run on synthetic frames with reference capture, results.csv, review.xlsx and images, option parsing).
- Core: `SunCalculatorTests` (against astropy), `CloudDetectorTests.SeveralRoisOnOneFrameAgreeWithSeparateRuns`.
- Review tool (pytest, `tests/review`): `test_review_workbook` (reading by headings, writing only My verdict with formulas and drop-down kept, refused while Excel has it open), `test_review_session` (start point, moving, filters, rows shown, going to a picked row, resuming, counts, saving later). C#: `RequirementTagScannerTests` (Python tags count towards coverage).

## Results

| Date | Frames | Result | Notes |
|---|---|---|---|
| | | | |

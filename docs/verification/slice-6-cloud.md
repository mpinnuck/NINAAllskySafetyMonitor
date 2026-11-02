# Slice 6 verification: cloud detection, sky-frame check, tuning mode

Requirements: REQ-28, REQ-29, REQ-30, REQ-31, REQ-41, REQ-43, REQ-45, REQ-50, REQ-60 (also REQ-37, REQ-44, REQ-57). Requirements v5.4.

The monitor now judges cloud in the ROI, so it can report **SAFE**.
- **Sky-frame check (REQ-60):** each image is decoded (darkest colour channel) and checked to be a sky frame. A flat, drawn image, such as Allsky's notice image, is unsafe with "Not a sky frame".
- **Star test (REQ-29):** catalogue stars to magnitude 4.4 that should be visible in the ROI are looked for in the image. They must be inside the working area and its outline, and more than 15° from the Moon. Finding fewer than 70% of them means cloud. With fewer than 8 expected stars the star test is skipped.
- **Brightness test (REQ-30):** compares the ROI with a clear-sky map, independent of exposure. It covers brightness relative to the rest of the working area, and non-uniformity. The map is captured automatically on the first clear, moonless frame after **Thresholds tuned** is ticked.
- **The Moon (REQ-31):** its position and phase come from Core and raise the brightness limits.
- **Tuning mode (REQ-41):** when on, saves every frame and a CSV row of metrics per cycle. Nights older than 30 days are deleted.
- **Fail to safe (REQ-50 v5.4):** a new switch, **Safe when the monitor can't tell**, off by default. When it is on, a condition that can't be determined is assumed safe and shown as **SAFE (ASSUMED)** in amber. That covers no usable image, not a sky frame, an uncalibrated lens, cloud unknown, or no current weather data. Detected cloud, rain or wind, and a monitor error, stay unsafe.
- **Status panel (REQ-45):** shows the metrics, the expected stars (green found, red missing) and tonight's graph.

Most checks need a dark night. Checks 8, 15, 16 and 18 need particular conditions (Moon up, Allsky stopped, cloud) and can be done when they come. Record results in `verification.xlsx`, sheet **Slice 6**.

## A. Mac / B. AstroPC

Build and test on the Mac (all pass, two skipped), commit and push. Then pull, rebuild and run Test Explorer on the AstroPC.

## C. NINA checks

| # | Step | Expected | REQ |
|---|---|---|---|
| 1 | Options → Plugins → Allsky Safety Monitor | New **Cloud detection** section: limiting magnitude **4.4** (your saved 4.0 is migrated), cloud below **70** %, minimum **8** stars, brightness **1.3**, non-uniformity **1.5**, Moon exclusion **15**, Thresholds tuned off, "Clear-sky reference: none yet…". New **Tuning mode** section: off, folder, **30** days | 29, 30, 31, 41, 43, 46 |
| 2 | **Calibrate from current image** (clear night), **Fit**, **Save** | Calibration saved as before. This also records the image size, which the sky-frame check uses | 19, 60 |
| 3 | Clear night, connect, mount tracking a target high in the sky | Panel: "Stars: n of m found (x%) · cloud below 70% · mag ≤ 4.4" with m ≥ 8 and x ≥ 70%; "Brightness: no clear-sky reference yet, test skipped"; Moon line. Reason "All conditions clear: safe in 4:00", then **SAFE** | 28, 29, 37, 45 |
| 4 | Look at the panel image, then click it | Small circles on stars inside the ROI: green found, red missing; red ones are few on a clear night. Viewer: **Expected stars** checkbox shows and hides them | 29, 45, 59 |
| 5 | Leave it running for an hour | **Tonight** graph: star fraction near 1, safe line high | 45 |
| 6 | Slew to a target low near the trees, or into cloud if there is any | Unsafe with "Cloud: k of m stars (y%)" when fewer than 70% are found; the graph shows the drop | 28, 29 |
| 7 | Set **Minimum expected stars** to 500, Save; then back to 8, Save | Unsafe: "Cloud unknown: only m stars expected in the ROI (need 500) and no clear-sky reference yet"; safe again 4 min after restoring | 29, 50 |
| 8 | Night with the Moon up | Moon line "Moon up a°, p% lit…"; a target near the Moon expects fewer stars than the same target with the Moon down | 31 |
| 9 | Tick **Thresholds tuned**, Save, on a clear moonless night | UNTUNED banner gone. Within a cycle or two the NINA log has "Clear-sky reference captured"; Options shows "Clear-sky reference: yyyy-mm-dd hh:mm"; the panel shows "Brightness 1.0x× clear sky (limit 1.30×) · non-uniformity …" | 30, 43, 57 |
| 10 | Restart NINA; Options | Same reference date (stored in settings.json); settings file still small and readable | 57 |
| 11 | **Clear the clear-sky reference** | "none yet…"; recaptured on the next clear moonless frame (tuned) | 30 |
| 12 | Tick **Save every frame and its metrics**, Save; wait 3 cycles | Tuning folder has a folder for tonight (date the night started) with allsky-*.jpg and tuning-yyyy-mm-dd.csv; it opens in Excel with stars, fractions, brightness, Moon, rain, gust | 41 |
| 13 | In the tuning folder create an empty folder dated 31 days ago (e.g. 2026-10-03); wait a cycle (next night, or restart NINA) | The folder is deleted; NINA log "Deleted tuning data for 2026-10-03 (older than 30 days)" | 41 |
| 14 | Open today's decision log | New columns Stars expected … Moon illumination and Assumed safe, filled | 44 |
| 15 | Stop Allsky on the Pi until it shows its notice image; start it again afterwards | Unsafe: "Not a sky frame (Allsky notice image?)"; back to normal after restarting Allsky | 60 |
| 16 | A cloudy or partly cloudy night, with the reference captured | Unsafe with "Cloud: …" while cloud crosses the ROI; thin cloud may show "Cloud: ROI x× brighter than clear sky"; safe 4 min after it clears | 28, 30, 38 |
| 17 | Options → Safety decision and log | New **Safe when the monitor can't tell**, unticked | 46, 50 |
| 18 | Tick it, Save. Then stop Allsky as in check 15, or anything that leaves no usable image | Reason "Assumed safe: Not a sky frame…: safe in 4:00", then **SAFE (ASSUMED)** on an amber banner; NINA reports safe; decision log "Assumed safe" column TRUE | 45, 50 |
| 19 | With it still ticked: a target in cloud (as check 6); then enter a wrong weather API key and Save, and restore it afterwards | Detected cloud is still **UNSAFE**; with the wrong key, "Assumed safe: No weather data: …" counts down to **SAFE (ASSUMED)** | 50 |
| 20 | Untick tuning mode, Thresholds tuned and Safe when the monitor can't tell, as you prefer; Save | Saved; UNTUNED shown again if untuned | 41, 43, 50 |

Unit tests behind this slice:
- Analysis: `SkyFrameCheckTests`, `RoiMaskBuilderTests`, `StarTestTests` (predictor, matcher, fraction test), `BrightnessTestTests` (background map, brightness test, Moon factor, reference capture), `CloudDetectorTests` (synthetic 1000 px frames: clear, thick cloud, cloud elsewhere, thin cloud, exposure ±40%, too few stars, Moon).
- Other areas: `MoonCalculatorTests` (against astropy), `TuningRecorderTests`, `MonitoringCycleCloudTests`, plus updates to the decision log, settings, store and migration tests.

## Results

| Date | NINA version | Result | Notes |
|---|---|---|---|
| | 3.3.0.1069 | | |

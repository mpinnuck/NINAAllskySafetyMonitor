# Slice 5a verification: mount, working area and ROI

Requirements: REQ-04 … REQ-07, REQ-23 … REQ-27, REQ-58; REQ-22 in part (the circle; the drawn outline comes with calibration in 5b).

Each cycle now reads the mount from NINA and places the **ROI**, a 15° circle on the sky around where the telescope points. RA/Dec is converted to alt/az from the site and the time; the mount's own Alt/Az are **not** used, only logged. The ROI must lie mostly inside the **working area** (a 60° circle centred 15° south of the zenith). When the mount can't be used (not connected, parked, not tracking, no position), or less than 50% of the ROI is inside the working area, the **whole working area** is judged instead and the panel says why. While the mount slews, the last decision is held.

The working area and ROI are drawn on the panel image. Until the lens is calibrated (5b) the drawing uses a rough lens model from the image size, so **positions on the image are approximate**; the ROI itself is computed on the sky and doesn't depend on the drawing. Cloud detection still fails until slice 6, so the state stays UNSAFE.

Record results in `verification.xlsx`, sheet **Slice 5a**. Daytime is fine: limit +90 and cycle interval 10 s (check 15 uses 60 s). The mount must be connected in NINA for checks 4–11 (the AM5, or NINA's telescope simulator if the rig isn't set up).

## A. Mac / B. AstroPC

Build and test on the Mac (all pass, two skipped), commit and push; pull, rebuild and Test Explorer on the AstroPC.

`Geometry/HorizonMaskDefinition.cs` is replaced by `WorkingAreaDefinition.cs`. An existing `settings.json` still loads (its old `Mask` entry is ignored and the working area gets its defaults); the next Save writes `WorkingArea` instead.

## C. NINA checks

| # | Step | Expected | REQ |
|---|---|---|---|
| 1 | Options → Plugins → Allsky Safety Monitor | New **Working area and ROI** section: centre alt 75°, az 180°, radius 60°, ROI radius 15°, minimum inside 50%. **Lens model** section with "Enter by hand" off | 22, 23, 58 |
| 2 | Set working area radius 120 → Save; then Revert | "Not saved: Working area radius must be between 5° and 90°." | 22, 46 |
| 3 | Mount **disconnected**; connect the Allsky Safety Monitor (limit +90, interval 10 s) | Panel: "Mount: not connected"; "Judging the whole working area · Mount not connected: judging the whole working area"; image shows the working area as a **green** line, N/E/S/W labels, legend says positions are approximate | 07, 26 |
| 4 | Connect the mount, **parked** | Next cycle: "Mount parked: judging the whole working area" | 05, 26 |
| 5 | Unpark, tracking **off** | "Mount not tracking: judging the whole working area" | 05, 26 |
| 6 | Tracking on; slew to a star high in the south (e.g. Acrux, or anything above 50° altitude between az 150° and 210°) | After the slew: "ROI 15° circle at alt … az … · 100% inside the working area"; green ROI circle with a cross. Mount line shows RA/Dec and "(JNow)". ROI alt/az agree with Sky Atlas / Stellarium for that star and time within **0.5°** | 04, 05, 23, 24 |
| 7 | Slew a long way (90° or more) while watching the panel | During the slew: reason "Mount slewing: holding last decision (…)", state unchanged; after the slew, the normal reason and the ROI at the new target. If the slew is too quick to see, look for an "Unsafe … Mount slewing" row in the decision CSV | 27 |
| 8 | Slew to a target low in the **north** (alt ≈ 30°, az ≈ 0°) | "Judging the whole working area · ROI 0% inside the working area: judging the whole working area"; ROI drawn as an **orange dotted** circle; working area green | 25, 58 |
| 9 | Minimum inside → **0** %, Save; wait a cycle. Then set it back to 50 and Save | With 0%: back to "ROI 15° circle … 0% inside the working area" (no fallback). With 50%: the fallback again | 58 |
| 10 | Change the working area to centre alt 60°, radius 40°, Save; wait a cycle; then restore 75 / 180 / 60 | The working-area circle on the image moves and shrinks; fraction inside changes | 22 |
| 11 | Mount line, and today's decision CSV in Excel | Mount line ends "mount reports alt … az … (not used)". CSV has new columns Mount, Mount RA/Dec, Mount reported Alt/Az, ROI Alt/Az, ROI mode, ROI inside (%), ROI note, filled in. The mount-reported Az may differ from the ROI Az (REQ-06): note any difference | 06, 44 |
| 12 | Untick "Show working area and ROI on the image", then tick it again | Plain image, then the overlay again | 45 |
| 13 | Optional: Lens model → **Enter the lens model by hand** on; adjust **North rotation** (and East clockwise) until N/E/S/W sit over the right landmarks; Save | Starting values come from the latest image size; the drawing follows the new values next cycle. Note the values that fit | 19 |
| 14 | Park the mount; disconnect the monitor | "Mount parked: judging the whole working area"; then NOT CONNECTED | 26 |
| 15 | Options: cycle interval **60 s**, mount check interval **2 s** (new row under the cycle interval), Save. Connect the monitor, then slew | Within a few seconds of the slew starting: "Mount slewing: holding last decision (…)"; within a few seconds of it ending, the ROI at the new target, without waiting for the 60 s cycle. NINA log has "Mount changed (slewing): re-evaluating now" lines. The image is still fetched only once a minute | 27 |

Unit tests behind this slice: `CoordinateConverterTests` (checked against astropy), `SkyGeometryTests`, `LensProjectionTests`, `WorkingAreaTests`, `MountStateInterpreterTests`, `RoiCalculatorTests`, `RoiBuilderTests`, `SlewHoldTests`, `MonitoringCycleTests`, `DecisionLoggerTests`.

## Results

| Date | NINA version | Result | Notes |
|---|---|---|---|
| | 3.3.0.1069 | | |

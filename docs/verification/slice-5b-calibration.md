# Slice 5b verification: lens calibration, working-area outline, image viewer

Requirements: REQ-19, REQ-20, REQ-21, REQ-22 (the outline), REQ-47, REQ-57, REQ-59.

The lens is now calibrated from catalogue stars in a clear frame. Stars are found on the darkest colour channel, so the camera's coloured noisy pixels are suppressed, and single hot pixels are removed by a median filter. A starting model is found automatically; if that fails you name and click two or three bright stars. The fit then matches catalogue stars (to magnitude 4.5) and reports the number of stars and the error, with a warning below 15 stars or above 0.2°. A large viewer window shows the image with the overlay and the altitude and azimuth under the mouse; in calibration mode it is also where the working-area outline is drawn.

Prototype on `allsky-20261020022133.jpg`: automatic start, **227 stars, 2.25 px RMS**, zenith at about (1074, 1227), north −5.6°.

Record results in `verification.xlsx`, sheet **Slice 5b**. Copy `allsky-20261020022133.jpg` (or any clear moonless archive frame) somewhere the AstroPC can open it.

## A. Mac / B. AstroPC

Build and test on the Mac (all pass, two skipped), commit and push; pull, rebuild and Test Explorer on the AstroPC.

## C. NINA checks

| # | Step | Expected | REQ |
|---|---|---|---|
| 1 | Options → Plugins → Allsky Safety Monitor | New **Lens calibration** section: "Not calibrated: positions on the image are estimated from the image size. No working-area outline drawn.", buttons **Calibrate from a frame…** and **Calibrate from current image**, warn below **15** stars and above **0.2°** | 20, 46, 47 |
| 2 | **Calibrate from a frame…** → `allsky-20261020022133.jpg` | Message "…taken 2026-10-20 02:21:33…"; a large **Allsky calibration** window opens over NINA with the frame, alt/az grid and working area | 20, 47, 59 |
| 3 | Press **Fit** | After a few seconds: "N stars matched, error x px (y°)" with N ≥ 25 and y ≤ 0.2°, no warning. Green circles sit on stars; Sirius, Canopus, Achernar, Rigel labelled; grid centred left of the image centre | 19, 20, 21 |
| 4 | Zoom (wheel) and pan (drag) around Orion and Canopus; double-click to reset | Stars stay inside their circles; the cursor line shows alt/az; at Canopus it reads about alt 52°, az 134° | 21, 59 |
| 5 | Set **Warn below matched stars** to 500 (no Save needed), press **Fit** again; then set it back to 15 | Orange warning "Only N stars matched (at least 500 expected)…"; the fit is still shown | 20 |
| 6 | Fallback: choose **Canopus** in Identify, tick **Click to place it**, click Canopus on the image; same for **Achernar** and **Rigel**; press **Fit from identified stars** | Magenta crosses at the clicks; the result matches check 3 within 1–2 px | 20 |
| 7 | Tick **Draw outline**, click round the visible sky (along the tree line, excluding the overlay text top-left), untick it | A blue outline follows the clicks; "Outline: n points" | 22, 47 |
| 8 | Click outside the window | Calibration window stays open (only Save or Cancel close it) | 59 |
| 9 | **Save** | Window closes; Options: "Calibrated from N stars in a frame taken 2026-10-20 02:21, error …. Working-area outline: n points." | 19, 57 |
| 10 | Restart NINA; Options page | Same calibration text (stored in settings.json) | 57 |
| 11 | Connect the monitor (limit +90); Allsky Safety panel | Overlay uses the calibrated lens: N/E/S/W where the fit puts them, the blue outline drawn, legend no longer says "approximate" | 19, 22, 45 |
| 12 | Click the panel image | Large **Allsky image** window with the live image and overlay; cursor alt/az; it follows new images. A click outside it, or Esc, closes it | 59 |
| 13 | Mount tracking a target low near the trees (inside the circle, outside the outline) | ROI line "% inside" is lower than before the outline; whole-area fallback when below 50% | 22, 25, 58 |
| 14 | **Calibrate from a frame…** with a file without a time in its name (copy and rename it `test.jpg`) | Message asks for a name with the local time; no window opens | 20 |

Unit tests behind this slice: `StarCatalogueTests`, `StarDetectorTests` (hot pixels ignored), `FrameTimeReaderTests`, `LevenbergMarquardtTests`, `InitialGuessSearchTests`, `LensFitterTests`, `LensCalibrationSessionTests` (synthetic frame end to end), `CalibrationQualityCheckTests`, settings defaults and validation.

## Results

| Date | NINA version | Result | Notes |
|---|---|---|---|
| | 3.3.x nightly (record exact build) | | |

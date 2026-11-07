# NINA Allsky Safety Monitor

This plugin analyses the latest Allsky image, checks weather-station observations, and reports a safety state to NINA through **Equipment > Safety Monitor**. NINA consumes the reported `IsSafe` value; sequence behavior depends on NINA's safety-monitor settings.

## Before connecting

1. Install and enable **Allsky Safety Monitor** in NINA's Safety Monitor device list.
2. Open the plugin options and configure an image source. Direct SFTP from the Allsky Pi is the default; a web image source is also available.
3. If weather checking is enabled, enter the Weather.com PWS station ID and account API key. The API key and optional SFTP password are stored in Windows Credential Manager. Older settings containing a plaintext API key are migrated when the options page opens.
4. Calibrate the lens from a saved clear-sky frame or the current image. Cloud assessment requires a calibrated lens; the image-size estimate is not used as a substitute.
5. Confirm the working area, review the image and weather status, and leave **Cloud decides safety** off until cloud detection has been tuned and evaluated.

For direct SFTP, configure the Pi host (default `allsky.local`, with an optional fallback IP), user, image path, SSH key file, and pinned host-key fingerprint. Password login is available but not recommended. Use **Test image** and **Test weather** before relying on the monitor.

## Safety states

| State | Meaning |
|---|---|
| **STARTING** | The monitor is connected but has not completed a cycle. NINA is told unsafe. |
| **IDLE** | The Sun is above the configured operating-window limit. This state is reported unsafe. |
| **UNSAFE** | A condition failed, a monitor error occurred, or the clear period has not elapsed. The reason is shown in the status view. |
| **SAFE** | All required conditions have remained clear for the configured safe-after period. |
| **SAFE (ASSUMED)** | The imager enabled assuming genuinely undetermined conditions safe. The status view marks this separately. |

Unsafe conditions include rain, excessive wind, an invalid/non-sky image, stale images or weather, and a monitoring-loop fault or timeout. When **Safe when the monitor can't tell** is enabled, only genuinely undetermined conditions may be assumed safe; known stale data and invalid images remain unsafe.

## Cloud detection

Cloud is advisory by default. The current cloud result is shown beside the overall state, but it affects safety only when **Cloud decides safety** is enabled. Cloud detection uses expected catalogue stars and, when available, a clear-sky brightness reference. The default ROI radius is 15 degrees; it follows the mount position and is constrained by the configured working area.

Until the minimum expected star count is reached, the star test is inconclusive. The checked-in snapshot does not contain the newer small-count probability rule described by requirement REQ-29; do not rely on that rule until the lost changes have been recovered and verified.

The image is fetched periodically; it is not a live video feed. An image that is not a sky frame, including an Allsky notice image, is unsafe regardless of whether cloud detection is advisory.

## Weather

Weather.com PWS observations provide precipitation rate and wind gust. Any reported precipitation is unsafe; wind gusts above the configured limit are unsafe. Weather observations older than the configured freshness limit, or no successful observation after the initial grace period, are known failures and cannot be assumed safe.

Weather checking can be disabled in options. If disabled, rain and wind are not checked; only disable it deliberately and understand that this removes those protections.

## Manual override

The status view provides a three-choice override selector:

- **Auto** — report the automatic decision.
- **Force Unsafe** — report unsafe until returned to Auto.
- **Force Safe** — tell NINA it is safe regardless of the sky. Use only for controlled testing and return to Auto immediately afterward.

The override is not saved and returns to Auto when NINA restarts.

## Image viewer and calibration

The status view displays the most recently fetched JPEG with working-area, ROI, and star overlays. Open the enlarged viewer to inspect it; the displayed image is the latest fetched frame, not a live feed. In calibration mode, click on the image to read the corresponding altitude and azimuth. Letterboxed areas outside the image do not map to sky coordinates.

Calibration uses the time encoded in the frame filename as local time. If the computer and Allsky Pi use different time zones, align their time-zone settings before calibrating.

## Troubleshooting

- **No image / stale image:** verify the selected source, host or URL, remote path, SSH key, host-key fingerprint, and Pi connectivity. A timeout is reported as a fetch error; repeated failures become unsafe.
- **Weather unavailable:** check the station ID, API key, Weather.com account access, and internet connection. The key must be the account API key, not the station upload key.
- **Cloud assessment unavailable:** calibrate the lens and confirm the image dimensions match the calibration. Check whether cloud detection is advisory or deciding safety.
- **The settings page reports a load error:** the original settings file is left intact and a backup is created on the next successful save. Preserve both files before attempting manual recovery.
- **A monitor loop error or timeout:** the state becomes unsafe. Disconnect and reconnect only after the underlying operation has stopped.

The offline replay harness can re-analyse saved Allsky frames and create a review workbook. Open it with `python3 tools/review/review.py <run-folder>`. In the review tool, press **S** to edit the working-circle centre and radius. In the editor, **↑/↓** move the centre altitude and **←/→** move its azimuth by 1°; hold **Shift** for 5° steps. The circle preview updates as you move it. **Apply** previews typed edits; navigating to another frame keeps the preview active. **Save** updates only the working-circle values in the settings file used for the replay and creates a backup (`.bak`, or timestamped if that backup already exists). Re-run the harness with the saved settings to check the analysis before copying the values into NINA.

## Compatibility note

The plugin manifest declares NINA 3.3.0.1057 as its minimum. This repository snapshot still pins an older `NINA.Plugin` package and .NET 8 target. Confirm the AstroPC's installed NINA version and align the build target before producing a release; compatibility has not been verified in a real NINA runtime.

See the [requirements](./requirements/requirements.md) and [design documents](./design/) for additional details.

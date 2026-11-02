# NINAAllskySafetyMonitor — Requirements

Version 5.3 · 2026-11-02

A safety monitor for NINA on the AstroPC. It analyses images from an Allsky camera, combines them with rain and wind data from a weather station, and reports a safe/unsafe status to NINA.

Defaults shown in *[brackets]* are configurable. How configurable values are stored is covered in §13.4.

---

# Functional requirements

## 1. NINA integration

- **REQ-01** Appears in NINA as a safety monitor device (**Equipment → Safety Monitor**), usable by NINA sequences in the same way as any other safety monitor.
- **REQ-02** Starts working when the device is connected in NINA and stops when it is disconnected or NINA closes. Nothing needs to be started or stopped separately, and nothing appears on the desktop outside NINA.
- **REQ-03** Reports the result of the safety decision (§9) to NINA as safe or unsafe.

## 2. Site and mount data

- **REQ-04** The site location is taken from the active NINA profile. If that isn't available, a configured location is used *[lat −33.713562, lon 151.09024, elev 134 m — from station ISYDNEY478]*.
- **REQ-05** The mount state is taken from NINA: connected, parked, tracking, slewing, RA (hours), Dec (degrees) and the coordinate epoch.
- **REQ-06** The mount-reported Altitude/Azimuth are **not** used for positioning. The ASI Mount driver reports Azimuth in the mount's own home-referenced frame (0° when pointing at the celestial pole), not the geographic azimuth the ASCOM standard defines, and the value can differ after power-on until the mount is homed. They are logged for diagnostics only.
- **REQ-07** When the mount reports no valid position (for example, disconnected), the position is treated as unavailable, not as an error.

## 3. Operating window

- **REQ-08** Computes sun altitude from the site location and current time.
- **REQ-09** Active only while the sun is below *[−18°]* (astronomical twilight).
- **REQ-10** Outside the active window it *idles*: no image fetching or analysis, and it reports **unsafe**.

## 4. Image acquisition

- **REQ-11** The image source is selectable *[Direct]*:
  - **Web**: a base URL *[https://oci.microconcepts.com.au/allsky/]* plus image file name *[allsky.jpg]*. The base URL is the Allsky web page; the image is a separate file in the same folder.
  - **Direct from the Allsky Pi** over SFTP *[file /home/pi/allsky/tmp/allsky.jpg, user pi]*. This avoids the Allsky WebUI login and keeps working if the internet drops. The file is Allsky app data, not served by the web server.
- **REQ-12** Direct access authenticates with an SSH key *(preferred, [%USERPROFILE%\.ssh\id_ed25519])* or a password.
- **REQ-13** The Pi host is *[allsky.local]*, with an optional fallback IP *[192.168.1.110]* in case name resolution fails.
- **REQ-14** Fetches a new image every *[60 s]* while active.
- **REQ-15** Determines the image capture time (file modification time, or the web server's last-modified time). If the newest image is older than *[10 min]*, reports **unsafe** (stale image).
- **REQ-16** For direct access, the file's modification time is checked before downloading, and the image is skipped if it is unchanged since the last fetch.
- **REQ-17** An image caught part-way through being written (an incomplete JPEG) is discarded and fetched again after *[5 s]*, up to *[3]* attempts.
- **REQ-18** If no image can be fetched for *[10 min]*, reports **unsafe**.
- **REQ-60** An image that is not a sky frame, such as the notice image Allsky shows when its software is not running, is recognised and reported **unsafe** with that reason.

## 5. Lens calibration and working area

- **REQ-19** Provides a one-off calibration that maps alt/az to Allsky image pixel positions: image centre, rotation (north direction), east/west orientation and the fisheye projection.
- **REQ-20** Calibration is done by matching identified catalogue stars in a real clear-sky frame. A calibration that matches fewer than a configurable number of stars *[15]*, or whose fit error is above a configurable limit *[0.2°]*, is reported with a warning.
- **REQ-21** Shows a verification overlay (catalogue stars and an alt/az grid drawn on the live image) so calibration accuracy can be checked by eye.
- **REQ-22** A **working area** defines the part of the sky the monitor can see and assess. It is a circle of configurable radius *[60°]* on the sky, centred at a configurable altitude and azimuth *[alt 75°, az 180°, i.e. 15° south of the zenith]*. It can be reduced further by a drawn outline that excludes trees, the house and the overlay text.

## 6. Region of interest

- **REQ-23** The ROI is a circle of radius *[15°]* on the sky, centred on the mount's current RA/Dec.
- **REQ-24** RA/Dec is converted to alt/az using the coordinate epoch (JNOW on this rig), the site location and the current time, then to Allsky image pixels using the calibration (REQ-19).
- **REQ-25** Any part of the ROI outside the working area (REQ-22) is excluded from analysis.
- **REQ-58** If less than a configurable fraction *[50%]* of the ROI lies inside the working area, the whole working area is used as the ROI instead, and the status says so.
- **REQ-26** The ROI falls back to the whole working area when the mount is not connected, is parked, is not tracking, or its position is unavailable.
- **REQ-27** While the mount is slewing, the current safety state is held and the ROI is updated once the slew finishes. The mount state is checked at a configurable interval *[2 s]*, independent of the image interval, and a change of mount state is acted on at once.

## 7. Cloud detection

- **REQ-28** **Any cloud in the ROI means unsafe.**
- **REQ-29** Primary test: predict which catalogue stars should be visible in the ROI (down to a limiting magnitude *[4.4]*), detect stars in the image, and compute the detected fraction. If the fraction falls below *[0.70]*, there is cloud. If fewer than *[8]* stars are expected in the ROI, this test is inconclusive and only REQ-30 applies.
- **REQ-30** Secondary test, comparing the ROI with a clear-sky reference: a map of sky brightness and non-uniformity over the working area, captured automatically on the first tuned clear night. Cloud lit by city glow appears brighter and blotchier than clear sky. Because the Allsky camera adjusts its exposure to keep overall image brightness steady, the test does not depend on exposure:
  - brightness of the ROI relative to the rest of the working area, both compared with the reference, above *[1.3×]* → cloud;
  - non-uniformity (standard deviation ÷ median, after removing stars) above *[1.5×]* the reference → cloud.
  Until a reference exists, this test is skipped and REQ-29 alone decides.
- **REQ-31** Stars within *[15°]* of the Moon are excluded from the expected list, and brightness thresholds are adjusted for lunar phase and altitude, to avoid false unsafes from moonlight.

## 8. Weather (WunderStation ISYDNEY478)

- **REQ-32** Polls the Weather.com PWS current-observations API for station *[ISYDNEY478]* every *[2 min]*, with metric units, authenticating with the account's **API key** (not the device/station upload key).
- **REQ-33** Values are read from `observations[0]`: `epoch` (observation time), `metric.precipRate` (mm/h), `metric.windGust` (km/h). Response format confirmed 2026-11-01.
- **REQ-34** Any precipitation rate above zero → **unsafe** immediately.
- **REQ-35** Wind gust above *[40 km/h]* → **unsafe**.
- **REQ-36** If the station data is older than *[15 min]* or unavailable → **unsafe**. Weather checking can be switched off *[on]*.

## 9. Safety decision

- **REQ-37** The overall status is safe only when all of these are true: active window, fresh image, no cloud in the ROI, no rain, wind below the limit, and weather data current.
- **REQ-38** Transition to **unsafe** is immediate when any condition fails.
- **REQ-39** Transition to **safe** happens only after all conditions have held continuously for *[4 min]*.
- **REQ-40** A manual override (Force Safe / Force Unsafe / Auto) is provided for testing, shown prominently and recorded in the log. It reverts to Auto when NINA restarts.

## 10. Tuning and offline analysis

- **REQ-41** A tuning mode saves every frame together with its computed metrics, in a format readable by Excel, so thresholds can be tuned from real nights. It can run alongside normal operation *[off]*. Saved tuning data older than *[30 days]* is deleted.
- **REQ-42** Saved frames can be re-analysed offline, without the rig or a live NINA session, to test threshold changes.
- **REQ-43** Until the thresholds have been tuned, conservative defaults (biased towards unsafe) are used and an "UNTUNED" warning is shown.

## 11. Logging

- **REQ-44** Keeps a rolling log *[7 days]* of state changes, metrics, errors and the reason for every safe/unsafe decision.

## 12. User interface (within NINA)

- **REQ-45** A status view shows:
  - the latest Allsky image with the ROI circle, working area, and detected vs expected stars overlaid;
  - a large SAFE / UNSAFE / IDLE indicator showing the reason for the current state;
  - current metrics: star fraction, brightness, rain, wind gust, image age, weather data age, and the countdown to safe;
  - a history graph of the metrics and the safe state for the night.
- **REQ-46** A settings view edits every configurable value.
- **REQ-47** A calibration view supports lens calibration (REQ-19–21) and defining the working area (REQ-22).
- **REQ-59** The Allsky image, with its overlay, can be viewed enlarged, and the altitude and azimuth of any point on it can be read.

---

# Non-functional requirements

## 13.1 Platform

- **REQ-48** Runs on the Windows AstroPC within NINA 3.x (currently the 3.3 nightly).

## 13.2 Reliability

- **REQ-49** A failure in the safety monitor (crash, hang, network fault) must not disrupt NINA or a running sequence.
- **REQ-50** Fail-safe: any failure, or any state that can't be determined, results in **unsafe**.
- **REQ-51** Every network operation (image fetch, weather poll) has a timeout, so a stalled connection can't hang the monitor.

## 13.3 Security

- **REQ-52** The Pi's host key is verified against a pinned fingerprint *[SHA256:xdCrpFUWOI55OZOikaDiy7ON48u/4+pcu7EpiK0Jrqg]*; a mismatch is reported and the image treated as unavailable.
- **REQ-53** An SFTP password, if used, is stored using Windows' credential storage, never in the settings file.
- **REQ-54** Passwords and API keys are never written to the log.

## 13.4 Settings

- **REQ-55** All configurable values persist between sessions.
- **REQ-56** Settings are stored in a JSON file.
- **REQ-57** Calibration results (REQ-19), the working area (REQ-22) and the clear-sky reference (REQ-30) are stored with the settings.

---

## Open items

- ~~Local image path~~ — confirmed: `/home/pi/allsky/tmp/allsky.jpg`.
- ~~Web image URL~~ — confirmed 2026-11-02: `https://oci.microconcepts.com.au/allsky/allsky.jpg` (`image.jpg` is not the current image).
- ~~SSH key login~~ — confirmed 2026-11-01: AstroPC key `%USERPROFILE%\.ssh\id_ed25519` (no passphrase) logs in to `pi@allsky.local`.
- ~~Mount units~~ — confirmed 2026-11-01: RA in hours, Dec in degrees; the driver's Azimuth is unreliable (REQ-06).
- ~~Weather API key~~ — confirmed working 2026-11-01.
- ~~Initial cloud thresholds~~ — starting defaults set in REQ-29–31; to be refined from tuning-mode data (REQ-41).

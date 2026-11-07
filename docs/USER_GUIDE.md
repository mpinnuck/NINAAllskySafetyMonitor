# NINAAllskySafetyMonitor User Guide

Welcome to NINA Allsky Safety Monitor! This guide will help you use the plugin during imaging sessions.

## Overview

The Allsky Safety Monitor watches three things:

1. **Rain & Wind** — from your weather station (automatic, always active)
2. **Cloud** — from your allsky camera (automatic detection, advisory or automatic mode)
3. **Your Imaging Window** — checks that NINA's active window hasn't changed

When any condition becomes unsafe, the plugin either:
- **Pauses your sequence** automatically (rain, wind, and optionally cloud)
- **Shows a warning** so you can decide (cloud in advisory mode)

---

## Quick Start

### Step 1: Enable the Plugin in NINA

1. Launch NINA
2. Go to **Equipment → Safety Monitor**
3. Look for **Allsky Safety Monitor** in the device list
4. Check the box to enable it
5. Restart NINA to load the plugin

### Step 2: Configure Your Weather Station

1. In NINA, go to **Equipment → Safety Monitor → Allsky Safety Monitor → Settings**
2. Navigate to the **Weather Station** section
3. Enter your **Weather.com Station ID** (11-digit code from your weather station)
4. Save the settings

### Step 3: Point to Your Allsky Camera

1. In settings, navigate to the **Allsky Camera** section
2. Enter the **URL** to your allsky camera's JPEG image
3. Leave other allsky settings at defaults (ROI diameter 150°, magnitude limit 4.4)
4. Save the settings

### Step 4: Run an Imaging Session

1. In NINA, set up your target and imaging sequence
2. Make sure your mount is at home position or on your first target
3. Click **Start** in your imaging sequence
4. Watch the safety monitor status indicator

---

## Understanding the Status Indicator

The safety monitor shows a large **SAFE** or **UNSAFE** indicator in the status panel.

### Meanings

| Status | What It Means | Auto-Pauses | What You Do |
|--------|---------------|-------------|-----------|
| 🟢 **SAFE** | All conditions OK | N/A | Imaging continues normally |
| 🔴 **UNSAFE** | A safety condition triggered | Yes (rain/wind) | Pause or fix the condition |
| ⚪ **IDLE** | Plugin initializing or waiting | No | Wait a few seconds |

### Why It Says "UNSAFE"

Hover over or click the status indicator to see the reason:
- **"No rain data"** — Weather station hasn't responded yet
- **"Rain detected"** — Your weather station reported rain
- **"Wind too high"** — Wind speed exceeded your configured limit
- **"Cloud in ROI"** — Cloud detected in region of interest (if enabled)
- **"Wrong window"** — NINA's active window changed (e.g., clicked away)

---

## Cloud Detection: Advisory vs. Automatic Mode

### Default: Advisory Only

By default, cloud detection is **advisory only**. This means:

✅ Cloud is detected and shown  
✅ You see "Cloud: 3 of 11 stars (27%)" in yellow text  
❌ Cloud does NOT automatically pause your sequence  
✅ You pause manually by clicking **Force Unsafe**

**Why?** Cloud detection on compressed JPEG images can produce false alarms, especially with moonlight or city glow. Manual control gives you the best experience.

### Optional: Automatic Cloud Pausing

To enable automatic cloud pausing:

1. Go to **Settings → Cloud Detection**
2. Enable the checkbox: **"Cloud decides safety"**
3. Save settings
4. Now cloud detection will automatically pause sequences

**When to use:** After tuning the detection thresholds during a night with reliable clear-sky reference data.

---

## Using Force Unsafe / Auto

### Pause Imaging Manually

1. Watch the allsky image and status display
2. When you see cloud approaching:
   - Click the **Force Unsafe** button
   - Your imaging sequence pauses immediately
3. The status shows **"FORCED UNSAFE"**

### Resume Imaging

1. Wait for the cloud to pass
2. Click the **Auto** button to return to automatic decision-making
3. Imaging resumes where it was paused

**Note:** You can only click Force Unsafe/Auto when in Auto mode. Toggle buttons let you switch between them.

---

## Cloud Detection Details

### How It Works

Every 60 seconds, the plugin:

1. **Downloads** the latest allsky image
2. **Identifies** which stars from a catalogue should be visible
3. **Counts** how many stars are actually detected
4. **Calculates** the ratio: `detected / expected`
5. **Decides:** If ratio < 70%, cloud is likely

### What "Cloud: 3 of 11 stars" Means

- **Expected stars:** 11 stars should be visible down to magnitude 4.4
- **Detected stars:** Only 3 stars were found in the image
- **Conclusion:** 27% = fewer than 70% → cloud likely
- **Color:** Yellow advisory text (not red emergency)

### Moon Phase Handling

When the Moon is visible:
- Stars too close to the Moon are excluded (glare)
- Brightness comparisons adjusted for lunar illumination
- More false positives expected with bright Moon

### Low Star Count Rule

When fewer than 8 stars are expected:
- Detection becomes more cautious
- Cloud is only called if count is very unlikely (< 1% in clear sky)
- This prevents false alarms from sparse regions

---

## Weather Station Setup

### Finding Your Station ID

1. Visit [weatherforumcommunity.net](https://weatherforumcommunity.net) or your weather station's web interface
2. Look for your **Station ID** (usually an 11-digit number)
3. Copy it and paste into plugin settings

### What Gets Checked

- **Rain:** Any detected rain → UNSAFE
- **Wind:** If exceeded configured limit (default 25 mph / 40 km/h) → UNSAFE

### Data Freshness

The plugin checks that weather data is current:
- Data older than 15 minutes is considered stale
- Stale data → UNSAFE (to protect equipment if station stops reporting)

---

## Troubleshooting

### Cloud Detection Not Appearing

**Symptom:** No cloud status shown in yellow text

**Solutions:**
1. Check settings → Cloud Detection is enabled
2. Verify allsky camera URL is correct (try pasting URL in browser)
3. Check that NINA's active window shows the Safety Monitor
4. Wait 60 seconds (detection runs every minute)
5. Check NINA's debug log for connection errors

### "No Rain Data" Keeps Showing

**Symptom:** Status always shows "No rain data"

**Solutions:**
1. Verify weather station ID is correct (check Weather.com portal)
2. Confirm your station has an active internet connection
3. Wait up to 5 minutes for first data to arrive
4. Check NINA's debug log for weather station connection errors

### Wind Triggering Too Easily

**Symptom:** Sequence pauses when wind is acceptable

**Solutions:**
1. Go to Settings → Weather Station
2. Increase the **Wind Limit** (default 25 mph)
3. Save and test with next imaging session

### Cloud Always or Never Detects

**Symptom:** Cloud detection seems stuck or too sensitive/insensitive

**Cause:** JPEG compression, city glow, or tuning thresholds

**Solutions:**
1. For testing: Temporarily disable cloud in settings
2. Use **advisory mode** (default) and manually pause instead
3. If you want to enable automatic: Capture clear-sky reference on a known clear night, wait for tuning to stabilize
4. Review cloud detection logs in NINA's debug output

---

## Best Practices

### During Imaging Sessions

✅ **DO:**
- Keep the Safety Monitor window visible on a secondary monitor
- Watch for cloud advisory text in yellow
- Use Force Unsafe when you see approaching clouds
- Click Auto to resume when sky clears

❌ **DON'T:**
- Click away from NINA (triggers "Wrong window" safety check)
- Cover the allsky camera lens
- Rely only on automatic detection for valuable imaging
- Ignore the status indicator during long sequences

### Before an Imaging Night

✅ **DO:**
- Verify weather station is online and reporting data
- Test allsky camera URL (paste in browser, image loads)
- Review your wind speed limit setting
- Check cloud detection mode (advisory or automatic)

❌ **DON'T:**
- Leave weather station unplugged or offline
- Image without watching if cloud detection is disabled
- Use very low wind limits that trigger on normal breezes

### With Cloud Detection Advisory Mode (Default)

✅ **DO:**
- Watch the allsky image continuously
- Use the "Cloud: N of M stars" advisory as a hint
- Make your own judgment about cloud
- Click Force Unsafe when you want to pause

❌ **DON'T:**
- Assume cloud advisory is definitive (it's advisory!)
- Ignore obviously building clouds waiting for detection
- Blame the plugin if you miss cloud because you weren't watching

---

## Settings Reference

### Weather Station

| Setting | Default | Example | Notes |
|---------|---------|---------|-------|
| Station ID | (empty) | 12345ABCDE | 11-char code from Weather.com |
| Wind Limit | 25 mph | 40 km/h | Triggers UNSAFE when exceeded |
| Check Weather | On | On/Off | Enable/disable weather checks |

### Allsky Camera

| Setting | Default | Example | Notes |
|---------|---------|---------|-------|
| Camera URL | (empty) | http://192.168.1.100/allsky.jpg | Must be accessible from NINA PC |
| ROI Diameter | 150° | 90–180° | Region around current RA/Dec to check |
| Magnitude Limit | 4.4 | 4.0–5.0 | Faintest stars to expect |

### Cloud Detection

| Setting | Default | Example | Notes |
|---------|---------|---------|-------|
| Enabled | On | On/Off | Enable/disable cloud checking |
| Decides Safety | Off | On/Off | **OFF** = advisory only; **ON** = automatic pause |
| Detection Threshold | 70% | 60–80% | Ratio of detected/expected stars |

---

## Getting Help

### Logs and Debugging

1. In NINA, enable **Debug** mode: View → Show Debug Output
2. Look for lines starting with `[AllskySafetyMonitor]`
3. Copy relevant log entries when reporting issues

### Common Log Messages

| Message | Meaning |
|---------|---------|
| `Allsky camera image received: 640×480` | Image successfully downloaded |
| `Cloud assessment: 8 of 12 expected stars` | Detection result (8/12 = 67% < 70% = cloud) |
| `Weather station data current (age 3 min)` | Weather data OK |
| `Weather station data stale (age 18 min)` | Weather data too old (will trigger UNSAFE) |
| `ROI at RA 05h10m, Dec +25° 34'` | Current mount position |

### Reporting Issues

When reporting a problem:
1. Include your weather station ID (optional, non-sensitive)
2. Include allsky camera URL (hide IP if private network)
3. Include relevant debug log lines
4. Describe what you expected vs. what happened
5. Include time/date of the issue

---

## FAQ

**Q: Can I use this without a weather station?**  
A: Yes, but you won't get rain/wind checks. Only cloud detection from the allsky camera will work.

**Q: Does the plugin work during daytime?**  
A: No. Cloud detection requires stars to be visible. Use it only at night. Weather station checks still work in daytime (though wind is usually more relevant at night).

**Q: What if my allsky camera goes offline?**  
A: The plugin will try to reconnect. After 3 failed attempts, it shows "Image stale" and the status becomes UNSAFE (to protect your equipment).

**Q: Can I use this with multiple mounts?**  
A: The plugin is tied to one mount position. If you slew to a different target, the plugin updates automatically and continues monitoring.

**Q: Why do I see "Cloud: 0 of 0 stars" sometimes?**  
A: This happens when the ROI (region around your mount's RA/Dec) contains no catalogue stars. It's inconclusive, so cloud detection doesn't decide safety.

**Q: Should I enable "Cloud decides safety" for testing?**  
A: Only if you've captured reliable clear-sky reference data and trust the detection thresholds. For general use, leave it OFF and use advisory mode.

**Q: What's the difference between "Wrong window" and "IDLE"?**  
A: **Wrong window**: NINA is still running but you clicked away (e.g., opened another app). **IDLE**: Plugin is initializing or hasn't received enough data to decide yet.

---

## Tips & Tricks

### Monitor the Allsky Image

Click on the allsky image displayed in the Safety Monitor window to see:
- The live camera feed
- ROI circle overlay (shows what region is being checked)
- Detected stars highlighted (if available)

### Use Force Unsafe Strategically

Instead of waiting for automatic cloud to trigger, use Force Unsafe proactively:
- When you see thin cloud on the horizon
- Before clouds fully block the ROI
- When Moon glare is creating too many false alarms

Then use Auto to resume imaging as soon as sky clears.

### Adjust Wind Limit Conservatively

Start with a higher limit (30+ mph) and lower it over time as you get comfortable with your mount's wind handling. Better to image in slight wind than to keep pausing.

### Check Weather Data Freshness

If you see "No rain data" or "Weather data stale":
1. Check your weather station's internet connection
2. Log into Weather.com portal to verify data upload
3. Restart the weather station if necessary
4. NINA will resume normal checks once data flows in

---

## Version History

**v1.0.0** (Nov 2026)
- ✅ Manual cloud detection mode (advisory by default)
- ✅ Weather station integration (rain & wind)
- ✅ Force Unsafe / Force Safe / Auto manual override
- ✅ Cloud decision toggle setting
- ✅ Low-star-count safety rule
- ✅ SAFE / UNSAFE / IDLE status indicator

---

## Support & Feedback

For issues, suggestions, or questions:
- Check the [GitHub Issues](https://github.com/mpinnuck/NINAAllskySafetyMonitor/issues) page
- Review the [Requirements Document](../docs/requirements/requirements.md) for technical details
- Consult the [Design Documentation](../docs/design/) for architecture details

---

**Last Updated:** November 2026  
**Plugin Version:** 1.0.0  
**NINA Compatibility:** 3.0.0+


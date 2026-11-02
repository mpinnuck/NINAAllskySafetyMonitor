# Slice 3 verification: weather

Requirements: REQ-32 … REQ-36, plus REQ-45 (weather line), REQ-46 (weather settings) and REQ-54 (API key never logged).

In this slice the monitor reads station ISYDNEY478 every 2 minutes while active and checks rain rate, wind gust and data age. The reason now lists **every** failed condition. With a good image and good weather the only failure left is "Cloud detection not implemented yet", so it is still never safe.

Record results in `verification.xlsx`, sheet **Slice 3**. Daytime: set the limit to +90.

## A. Mac

1. `dotnet build NINAAllskySafetyMonitor.slnx` — 0 errors.
2. `dotnet test tests/NINAAllskySafetyMonitor.Tests` — all pass, two coverage tests skipped.
3. Commit and push.

## B. AstroPC

1. Close NINA, pull, rebuild: 0 errors.
2. Test Explorer → Run All: all pass, two skipped.

## C. NINA checks

| # | Step | Expected | REQ |
|---|---|---|---|
| 1 | Options → Plugins → Allsky Safety Monitor → Weather station | Poll interval 120 s, gust limit 40 km/h, stale after 15 min, timeout 15 s; **Test weather** button | 46 |
| 2 | Press **Test weather** | "Weather OK (ISYDNEY478): rain 0.0 mm/h, gust N km/h, observed hh:mm (x min ago)". Compare with the station's Wunderground page | 32, 33 |
| 3 | Connect; Allsky Safety panel | Weather line "Rain 0.0 mm/h · Gust N km/h · observed hh:mm (x min ago)"; reason just "Cloud detection not implemented yet" | 32, 45 |
| 4 | Watch 10 minutes | The observed time moves on as the station reports; the age stays within a few minutes | 32 |
| 5 | When there is some wind: set gust limit below the current gust (e.g. 0), Save; wait up to 2 min | Reason includes "Gust N km/h (limit 0 km/h)". Restore 40 and Save | 35 |
| 6 | Set "Weather data is stale after" to 1 min, Save | Within a cycle: "Weather data is x min old (limit 1 min)". Keep it at 1 for checks 7–8 | 36 |
| 7 | Change one character of the API key; press **Test weather**; then Save and wait up to 2 min | Test: "Weather failed: Weather API key was rejected (HTTP 401)" (or 403). Reason gains "(last poll failed: Weather API key was rejected …)". Restore the key and Save | 32, 54 |
| 8 | Station ID `IXXXXX000`, Test weather | A clear failure (no observation, or HTTP error), no crash. Restore `ISYDNEY478`; set stale limit back to 15; Save | 32, 36 |
| 9 | Untick "Check rain and wind", Save | Weather line disappears; no weather reasons. Tick it again and Save | 36 |
| 10 | If it rains during testing | Reason includes "Rain x.x mm/h" at once (otherwise covered by unit tests) | 34 |
| 11 | `.\Check-NinaLog.ps1 -Secret <first 8 characters of the API key> -Tail 40` | "Weather fetch failed: …" warnings from checks 7–8; the key **PASS** (not found) | 54 |
| 12 | Limit −18 in daylight (or after the session) | IDLE; no weather line; no weather requests | 10 |

## Results

| Date | NINA version | Result | Notes |
|---|---|---|---|
| | 3.3.0.1069 | | |

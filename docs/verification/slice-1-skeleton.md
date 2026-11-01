# Slice 1 verification: skeleton

Requirements: REQ-01, 02, 03, 07, 08, 09, 10, 46 (partial), 48, 49, 50, 55, 56.

In this slice the monitor connects, works out the sun altitude and reports **Idle (unsafe)** outside the operating window. Inside the window it reports **Unsafe: "Image acquisition not implemented yet"**. It is never safe in slice 1. That is expected.

## A. Mac

1. Build the solution in VS Code. The Plugin project may not build on the Mac (WPF XAML needs Windows); Core, Astrometry and Tests must build.
2. Run the tests: `dotnet test tests/NINAAllskySafetyMonitor.Tests --logger "console;verbosity=normal"`. All tests pass; the two coverage tests are skipped.
3. Commit and push.

## B. AstroPC build

1. Close NINA.
2. In Visual Studio: Git → Pull, then Build → Rebuild Solution. 0 errors.
3. Test Explorer → Run All. All pass (two skipped).
4. Check that `%LOCALAPPDATA%\NINA\Plugins\3.0.0\NINAAllskySafetyMonitor\` contains `NINAAllskySafetyMonitor.dll`, `NINAAllskySafetyMonitor.Core.dll` and `NINAAllskySafetyMonitor.Astrometry.dll`.

## C. NINA checks

| # | Step | Expected | REQ | Pass |
|---|---|---|---|---|
| 1 | Start NINA. Options → Plugins → Installed. | "Allsky Safety Monitor" is listed and NINA starts with no plugin error. | 48 | |
| 2 | Open the plugin's options page. | Operating window, interval, fallback site, weather station fields, Save and Revert buttons, settings file path. | 46 | |
| 3 | Equipment → Safety Monitor → device list. | "Allsky Safety Monitor" is offered. | 01 | |
| 4 | Imaging tab → panels menu → enable "Allsky Safety". | Panel shows **NOT CONNECTED** (grey). | 45 (partial) | |
| 5 | Connect the device in daytime, limit at −18°. | Within one interval: panel shows **IDLE** (red), reason "Outside operating window: Sun at x° (limit −18°)". The NINA safety monitor shows **Unsafe**. | 02, 03, 08, 09, 10 | |
| 6 | Compare the Sun altitude in the reason with Stellarium or the NINA Sky Atlas for now. | Agrees within about 1°. | 08 | |
| 7 | Set the limit to +90, Save. | Message "Saved hh:mm:ss". Within one interval the reason changes to "Image acquisition not implemented yet", state **UNSAFE**. NINA still shows Unsafe. | 09, 50 | |
| 8 | Set the interval to 10, Save; then set it to 5, Save. | 10 is accepted and updates arrive every 10 s. 5 is rejected with "Not saved: Image interval must be between 10 s and 3600 s."; the saved value stays 10. Set it back to 60 afterwards. | 46 | |
| 9 | Change a field, press Revert. | The field returns to the saved value. | 46 | |
| 10 | Open the settings file shown on the options page. | JSON, readable, contains the values just saved, contains no password. | 56 | |
| 11 | Restart NINA, reopen the options page. | The saved values are still there. | 55 | |
| 12 | Disconnect the device. | Panel shows **NOT CONNECTED**. NINA shows the monitor disconnected. No windows outside NINA at any point. | 02 | |
| 13 | Reconnect, then close NINA while connected. | NINA closes normally with no hang. | 02, 49 | |
| 14 | Disconnect the mount (or leave it disconnected) while the safety monitor is connected. | The safety monitor keeps working; no errors from it. | 07, 49 | |
| 15 | In the NINA profile, clear the site latitude and longitude (to 0); reconnect. | Reason still shows a sensible Sun altitude, from the fallback site. Restore the profile afterwards. | 04 (preview) | |
| 16 | Open the NINA log (`%LOCALAPPDATA%\NINA\Logs`) and search for `[AllskySafetyMonitor]`. | Connect, disconnect and state messages present; no exceptions; the weather API key does not appear. | 49, 54 | |
| 17 | Set the limit back to −18 and Save. | | | |

Record the results in the Verified column of `docs/design/traceability.xlsx` (to be added) and note any failures here.

## Results

| Date | NINA version | Result | Notes |
|---|---|---|---|
| | 3.3.0.1057 | | |

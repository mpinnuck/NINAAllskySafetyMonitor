# Slice 2 verification: image

Requirements: REQ-11 … REQ-18, REQ-45 (image part), REQ-51 … REQ-53.

In this slice the monitor fetches the Allsky image while active, checks it is whole and fresh, and shows it in the status panel. It is still never safe: with a fresh image the reason is "Cloud detection not implemented yet · Image x min old".

Record results in `verification.xlsx`, sheet **Slice 2**.

## A. Mac

1. `dotnet build NINAAllskySafetyMonitor.slnx` — 0 errors. The first build downloads SSH.NET.
2. `dotnet test tests/NINAAllskySafetyMonitor.Tests` — all pass, two coverage tests skipped.
3. Commit and push.

## B. AstroPC build

1. Close NINA. Git → Pull. Build → Rebuild Solution: 0 errors.
2. Test Explorer → Run All: all pass, two skipped.
3. The plugin folder `%LOCALAPPDATA%\NINA\Plugins\3.0.0\NINAAllskySafetyMonitor\` now also contains `Renci.SshNet.dll`, `BouncyCastle.Cryptography.dll` and `Microsoft.Extensions.Logging.Abstractions.dll` (SSH.NET 2026.0.0 and its dependencies).

## C. NINA checks

Do these at night, or with the limit at +90 in daytime.

| # | Step | Expected | REQ |
|---|---|---|---|
| 1 | Options → Plugins → Allsky Safety Monitor | New Image source, Direct (SFTP) and Web sections, including Host-key type = `ecdsa-sha2-nistp256`; earlier settings unchanged; Test image button | 46 |
| 2 | Source = Direct, press **Test image** | "OK (Direct): N KB, captured hh:mm:ss (x min ago), complete JPEG" | 11, 12, 52 |
| 3 | Connect the safety monitor; Imaging tab → Allsky Safety | Within a minute: the Allsky image, "Image captured hh:mm:ss (x min ago) · Direct · N KB"; UNSAFE, "Cloud detection not implemented yet · Image x min old" | 11, 14, 15, 45 |
| 4 | Watch for 3 minutes | Updated time moves every minute; captured time moves on as Allsky takes new pictures; the age counts up between them | 14, 15 |
| 5 | Set Host to `allsky-wrong.local`, Save; wait one cycle | Image still updates (through the fallback IP). NINA log: "Could not reach allsky-wrong.local …; trying 192.168.1.110". Restore Host to `allsky.local` and Save | 13 |
| 6 | Change one character of the pinned host-key fingerprint, Save; wait one cycle | Image disappears; UNSAFE, "No image: Host key mismatch for allsky.local: received ssh-ed25519 SHA256:xdCr…". Test image reports the same. Restore the fingerprint and Save | 52 |
| 7 | Set "Unsafe when no fetch succeeds for" to 1 min; set Host to `nohost.local` and Fallback IP to `192.168.1.250`; Save | First cycle: still UNSAFE with "… · Image x min old (last fetch failed: Could not connect …)". After a minute: "No image fetched for 1.x min (limit 1 min): …". NINA stays responsive; each failing fetch ends within the 20 s timeout | 18, 51 |
| 8 | Restore Host, Fallback IP and the 1 min limit (10), Save | Image back within a minute | 13, 18 |
| 9 | On the Pi: `sudo systemctl stop allsky`. Wait 11 minutes | Captured time stops moving; after 10 minutes: "Image is 10.x min old (limit 10 min)". Then `sudo systemctl start allsky` | 15, 16 |
| 10 | Source = Web, press Test image, then Save | "OK (Web): …"; panel image info shows "· Web". Set Source back to Direct and Save | 11 |
| 11 | Tick "Log in with a password instead", type any password, Save. Then untick it and Save | Status line: "A password is stored in Windows Credential Manager". `settings.json` has no password. Control Panel → Credential Manager → Windows Credentials shows `NINAAllskySafetyMonitor/sftp-password` (remove it there afterwards) | 53 |
| 12 | NINA log, search `AllskySafetyMonitor`, then search for the password from check 11 and the first characters of the API key | Image-fetch warnings from checks 5–8; neither secret appears | 54 |
| 13 | Daytime, limit −18 (or set the limit to −30 at night) | IDLE; no image shown; nothing fetched | 10 |

Incomplete JPEGs (REQ-17) can't be produced on demand; they are covered by the unit tests. If one happens, the log shows "Incomplete image (attempt n of 3)".

## Results

| Date | NINA version | Result | Notes |
|---|---|---|---|
| | 3.3.x nightly (record exact build) | | |

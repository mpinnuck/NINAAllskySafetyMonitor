# NINAAllskySafetyMonitor

A NINA safety-monitor plugin. It analyses the latest image from an Allsky camera for cloud in a circle around the mount's current RA/Dec, checks rain and wind gusts from a Weather.com personal weather station, and reports safe or unsafe to NINA through **Equipment → Safety Monitor**.

## Quick Links

- 📖 **[User Guide](docs/USER_GUIDE.md)** — For NINA imagers: how to set up and use the plugin
- 📋 **Requirements:** [`docs/requirements/requirements.md`](docs/requirements/requirements.md) (v5.10, REQ-01 … REQ-64)
- 🏗️ **Design:** [`docs/design/`](docs/design/) — PDF design document, PlantUML sources, traceability matrix

## Solution layout

| Project | Target | Role |
|---|---|---|
| `src/NINAAllskySafetyMonitor.Core` | net8.0 | All safety logic. No NINA references. |
| `src/NINAAllskySafetyMonitor.Astrometry` | net8.0-windows | Adapter over NINA.Astrometry (sun, Moon, RA/Dec ↔ alt/az). |
| `src/NINAAllskySafetyMonitor.Plugin` | net8.0-windows (WPF) | The NINA plugin: provider, device, adapters, views. |
| `tools/NINAAllskySafetyMonitor.Harness` | net8.0 | Console: cross-platform offline replay of saved frames and review workbook. |
| `tools/review` | Python | Offline review application for the harness workbook and marked-up frames. |
| `tests/NINAAllskySafetyMonitor.Tests` | net8.0 | xUnit tests for Core and requirement-coverage tests. |
| `tests/review` | Python | Tests for the offline review application. |

Folders inside Core and Tests match the design packages (CL-01): one folder per package, one class per file, namespace = folder.

## Workflow

1. **Mac:** generate and edit code, review against the design, commit and push to GitHub.
2. **AstroPC (Windows 11):** in Visual Studio 2026, clone or pull from GitHub, open `NINAAllskySafetyMonitor.slnx`, build and test.
3. A Debug or Release build of the Plugin project copies `NINAAllskySafetyMonitor*.dll` to the configured NINA plugin folder. Restart NINA to load it.

## Offline replay and simulated mounts

The harness can replay archived Allsky frames as if a mount were tracking a nominated target:

```bash
# One target through the night
dotnet run --project tools/NINAAllskySafetyMonitor.Harness -- replay \
  --frames "~/AllskyFrames" --settings "~/AllskyFrames/settings.json" \
  --out "~/AllskyFrames/Replay/M42" --zone Australia/Sydney --step 300 \
  --target "M42=05:35,-05:23" \
  --save-settings "~/AllskyFrames/settings-tuned.json"

# Several nominated targets (repeat --target)
dotnet run --project tools/NINAAllskySafetyMonitor.Harness -- replay \
  --frames "~/AllskyFrames" --settings "~/AllskyFrames/settings.json" \
  --out "~/AllskyFrames/Replay/targets" --zone Australia/Sydney --step 300 \
  --target "M42=05:35,-05:23" --target "M31=00:42,+41:16"

# A grid of ROIs, Dec -50° to +50° every 10°, RA every hour
dotnet run --project tools/NINAAllskySafetyMonitor.Harness -- replay \
  --frames "~/AllskyFrames" --settings "~/AllskyFrames/settings.json" \
  --out "~/AllskyFrames/Replay/dec50" --zone Australia/Sydney --step 300 \
  --dec -50:50:10 --ra-step 1
```

`--target` takes `RA,Dec`; RA is decimal hours or `hh:mm[:ss]`, Dec is decimal degrees or `±dd:mm[:ss]`. A name is optional: `name=RA,Dec`. When any `--target` is supplied, the RA/Dec grid is not used. Targets below the horizon or outside the working area are skipped at that frame; their ROIs are rebuilt at every frame time exactly as the plugin builds them. `--nights` limits the nights, and the Sun-altitude operating window limits replayed frames to dusk–dawn. `--save-settings` writes a copy of the settings with the clear-sky reference captured during the replay, so the archived run can produce a ready-to-use settings file.

See [`docs/ARCHIVED_TUNING.md`](docs/ARCHIVED_TUNING.md) for the archived-data tuning workflow.

## Before the first build

- The compile-time baseline is stable `NINA.Plugin` 3.2.0.9001 with `net8.0-windows`; the plugin manifest supports NINA 3.2.0 and later. The AstroPC currently runs a 3.3.x nightly; post-build copies target its `3.0.0` plugin folder. Verify newer-host compatibility and smoke-test the release candidate on that runtime, recording its exact nightly build.
- Confirm the licence and repository URL in `src/NINAAllskySafetyMonitor.Plugin/Properties/AssemblyInfo.cs`.

## Traceability

The traceability scanner collects `[Requirement("REQ-xx")]` tags from C# and Python, including combined C# attributes. Unknown IDs and unexpected implementation/test coverage gaps fail the test suite. Explicit exceptions document UI/platform requirements that need manual NINA verification.

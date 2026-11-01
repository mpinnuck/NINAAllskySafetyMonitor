# NINAAllskySafetyMonitor

A NINA safety-monitor plugin. It analyses the latest image from an Allsky camera for cloud in a circle around the mount's current RA/Dec, checks rain and wind gusts from a Weather.com personal weather station, and reports safe or unsafe to NINA through **Equipment → Safety Monitor**.

- Requirements: [`docs/requirements/requirements.md`](docs/requirements/requirements.md) (v5, REQ-01 … REQ-58)
- Design: [`docs/design/`](docs/design/) — PDF design document, PlantUML sources, traceability matrix

## Solution layout

| Project | Target | Role |
|---|---|---|
| `src/NINAAllskySafetyMonitor.Core` | net8.0 | All safety logic. No NINA references. |
| `src/NINAAllskySafetyMonitor.Astrometry` | net8.0-windows | Adapter over NINA.Astrometry (sun, Moon, RA/Dec ↔ alt/az). |
| `src/NINAAllskySafetyMonitor.Plugin` | net8.0-windows (WPF) | The NINA plugin: provider, device, adapters, views. |
| `tools/NINAAllskySafetyMonitor.Harness` | net8.0-windows | Console: offline replay of saved frames, traceability report. |
| `tests/NINAAllskySafetyMonitor.Tests` | net8.0 | xUnit tests for Core and requirement-coverage tests. |

Folders inside Core and Tests match the design packages (CL-01): one folder per package, one class per file, namespace = folder.

## Workflow

1. **Mac:** generate and edit code, review against the design, commit and push to GitHub.
2. **AstroPC (Windows 11):** in Visual Studio 2026, clone or pull from GitHub, open `NINAAllskySafetyMonitor.slnx`, build and test.
3. A Debug or Release build of the Plugin project copies `NINAAllskySafetyMonitor*.dll` to `%LOCALAPPDATA%\NINA\Plugins\3.0.0\NINAAllskySafetyMonitor`. Restart NINA to load it.

## Before the first build

- Set `NinaPluginVersion` in `Directory.Build.props` to the `NINA.Plugin` package version used by RATrendCompensatorNINA (proven with NINA 3.3.0.1057).
- Confirm the licence and repository URL in `src/NINAAllskySafetyMonitor.Plugin/Properties/AssemblyInfo.cs`.

## Traceability

Every class and test carries `[Requirement("REQ-xx")]`. The coverage tests fail the build if a requirement has no implementing class or no test, or if a tag names an unknown requirement.

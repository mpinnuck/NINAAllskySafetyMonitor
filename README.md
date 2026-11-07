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

## Before the first build

- Confirm the AstroPC's installed NINA version, then align `NinaPluginVersion`, the Plugin and Astrometry target frameworks, and the plugin output folder. This snapshot declares a minimum of NINA 3.3.0.1057 but still pins `NINA.Plugin` 3.0.0.2017-beta and targets .NET 8; it is not release-verified.
- Confirm the licence and repository URL in `src/NINAAllskySafetyMonitor.Plugin/Properties/AssemblyInfo.cs`.

## Traceability

The traceability scanner collects `[Requirement("REQ-xx")]` tags from C# and Python, including combined C# attributes. Unknown IDs and unexpected implementation/test coverage gaps fail the test suite. Explicit exceptions document the unrecovered REQ-29 low-count rule and UI/platform requirements that need manual NINA verification.

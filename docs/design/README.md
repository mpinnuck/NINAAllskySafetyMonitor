# Design v1.4 (2026-11-02)

Traced to `../requirements/requirements.md` v5.2 (REQ-01 … REQ-59). Version 1.4 matches slices 1–5 as built and verified; §1.5 of the PDF lists the changes from v1.3.

| File | Contents |
|---|---|
| `uml/*.puml` | PlantUML sources in Rational Rose style (`uml/_style.iuml`). These are the editable masters. |
| `review/NINAAllskySafetyMonitor-Design-v1.4.pdf` | Software Design Document for review, generated from the sources. Earlier versions are in `review/superseded/`. |
| `review/traceability.xlsx` | REQ → use case → design class → scenario, generated from the sources. Do not edit by hand. |

Diagram index:

- **Use cases:** UC01 operation, UC02 set-up and tuning, UC03a/UC03b requirement trace.
- **Classes:** CL01 packages, CL02 domain, CL03a ports, CL03b settings, CL04 acquisition, CL05 weather, CL06 geometry, CL07 cloud detection, CL08 decision and monitoring, CL09 calibration/tuning/logging/traceability, CL10a plugin NINA integration, CL10b plugin view models and image viewer, CL11 harness and tests.
- **Scenarios:** SD01 connect, SD02 monitoring cycle, SD03 cloud detection, SD04 hysteresis, SD05 slew and fallback, SD06 acquire image, SD07 failure and idle, SD08 calibration, SD09 offline replay, SD10 override and disconnect, SD11 status and settings, SD12 image viewer.

## Rendering

In VS Code install the **PlantUML** extension (jebbs.plantuml) plus Java and Graphviz (`brew install openjdk graphviz`). Open a `.puml` file and press Alt+D to preview. Rendered output goes to `svg/` or `png/`, which are git-ignored.

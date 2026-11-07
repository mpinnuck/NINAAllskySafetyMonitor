# Slice 4 verification: safety decision

Requirements: REQ-37 … REQ-40, REQ-44, REQ-54; REQ-03 and REQ-46 again.

Every cycle's conditions now go through the decision: **unsafe at once** when any condition fails, **safe only after every condition has been clear for 4 minutes**, with a countdown. A manual override (Auto / Force Safe / Force Unsafe) is on the status panel, and every decision is written to a daily CSV log.

Cloud detection still always fails until slice 6, so the monitor itself never reaches SAFE yet. The countdown and the 4-minute rule (REQ-38, REQ-39) are covered by unit tests now and are seen in NINA in slice 6. **Force Safe** is used here to check that NINA acts on a safe signal.

Record results in `verification.xlsx`, sheet **Slice 4**. Daytime: limit +90.

## A. Mac / B. AstroPC

Build and test on the Mac (all pass, two skipped), commit and push; pull, rebuild and Test Explorer on the AstroPC.

## C. NINA checks

| # | Step | Expected | REQ |
|---|---|---|---|
| 1 | Options → Plugins → Allsky Safety Monitor | New **Safety decision and log** section: safe after 4 min, keep log 7 days, log folder | 46 |
| 2 | Connect; Allsky Safety panel | **Override: Auto** at the bottom; UNSAFE "Cloud detection not implemented yet" | 37 |
| 3 | Override → **ForceSafe** | At once: orange "MANUAL OVERRIDE: FORCE SAFE…" banner, state **SAFE** (green), reason "MANUAL OVERRIDE: Force Safe (monitor says Unsafe: …)". Equipment → Safety Monitor: **Is Safe ✓** within a few seconds | 03, 40 |
| 4 | Override → **ForceUnsafe** | Banner "FORCE UNSAFE"; UNSAFE; Is Safe ✗ | 40 |
| 5 | Override → **Auto** | Banner gone; normal UNSAFE reason | 40 |
| 6 | Optional: Advanced Sequencer, a sequence with **Wait until Safe** then a short **Wait for Time Span**. Run it with Override = ForceUnsafe, then switch to ForceSafe | The sequence waits while unsafe and continues within seconds of Force Safe. Set Auto afterwards | 01, 03 |
| 7 | Set ForceUnsafe; disconnect and reconnect | Still ForceUnsafe (kept for the session) | 40 |
| 8 | Restart NINA, connect | Override is **Auto** again | 40 |
| 9 | NINA log (`.\Check-NinaLog.ps1 -Tail 40`) | "Manual override set to Force Safe / Force Unsafe / Auto" lines | 40 |
| 10 | Open the decision log folder; open today's `decisions-2026-11-02.csv` in Excel | Header row; one row per cycle; Event "State change" where the state changed (e.g. at Force Safe); image and weather metrics filled in | 44 |
| 11 | Copy today's file to `decisions-2026-10-20.csv` in the same folder; disconnect and reconnect; wait one cycle | `decisions-2026-10-20.csv` is deleted (older than 7 days) | 44 |
| 12 | `.\Check-NinaLog.ps1 -Tail 0` with the API key at the prompt | **PASS**: the key is in neither the NINA logs nor the decision logs | 54 |
| 13 | Limit −18 in daylight | IDLE; decision log rows say Idle | 10, 44 |

The safe path itself (REQ-38, REQ-39): `SafetyHysteresisTests`, `SafetyDecisionEngineTests` and `MonitorServiceTests.DecisionEngineDecidesThePublishedStatus`.

## Results

| Date | NINA version | Result | Notes |
|---|---|---|---|
| | 3.3.x nightly (record exact build) | | |

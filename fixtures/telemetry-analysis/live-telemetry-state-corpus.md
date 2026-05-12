# Live Telemetry State Corpus

Compact redacted states derived from local raw captures for Standings, Relative, and Gap To Leader source-selection work.

## States

| ID | Capture | Session | Phase | Focus | Standings | Relative | Gap | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ai-practice-no-valid-lap | capture-20260511-001730-564 | Practice | green | player | none; rows 0; valid 0; render False | waiting; fallback rows 0 | unavailable:gap_signals_missing; data False | Standings should wait here because practice/qualifying/test requires a valid lap. |
| ai-qualifying-valid-lap-gated | capture-20260511-001730-564 | Lone Qualify | post-green | non-player | session-results; rows 41; valid 38; render True | model-v2-timing-fallback; fallback rows 60 | reliable:position; data True | Practice/qualifying standings can render because selected scoring rows include valid lap times. Relative depends on model-v2 timing fallback rather than local proximity in this state. |
| ai-race-pre-green | capture-20260511-001730-564 | Race | pre-green | non-player | starting-grid; rows 41; valid 38; render True | model-v2-timing-fallback; fallback rows 63 | unavailable:gap_signals_missing; data False | AI race state does not have usable local-player context; Standings/Relative/Gap must use focus/timing/scoring arrays. Relative depends on model-v2 timing fallback rather than local proximity in this state. Race pre-green may expose grid/scoring before live race gaps are meaningful. |
| ai-race-green-non-player-focus | capture-20260511-001730-564 | Race | green | non-player | session-results; rows 40; valid 40; render True | model-v2-timing-fallback; fallback rows 63 | reliable:CarIdxF2Time; data True | AI race state does not have usable local-player context; Standings/Relative/Gap must use focus/timing/scoring arrays. Relative depends on model-v2 timing fallback rather than local proximity in this state. |
| open-practice-non-player-focus | capture-20260511-002956-343 | Practice | green | non-player | session-results; rows 14; valid 14; render True | model-v2-timing-fallback; fallback rows 63 | reliable:CarIdxF2Time; data True | Practice/qualifying standings can render because selected scoring rows include valid lap times. Relative depends on model-v2 timing fallback rather than local proximity in this state. |
| open-practice-player-focus | capture-20260511-002956-343 | Practice | green | player | none; rows 0; valid 0; render False | waiting; fallback rows 0 | unavailable:gap_signals_missing; data False | Standings should wait here because practice/qualifying/test requires a valid lap. |
| endurance-4h-race-running | capture-20260426-130334-932 | Race | green | player | session-results; rows 58; valid 55; render True | live-proximity; fallback rows 63 | reliable:CarIdxF2Time; data True |  |
| endurance-4h-pit-or-garage | capture-20260426-130334-932 | Race | green | player | session-results; rows 58; valid 55; render True | model-v2-timing-fallback; fallback rows 63 | reliable:CarIdxF2Time; data True | Relative depends on model-v2 timing fallback rather than local proximity in this state. |
| endurance-24h-race-running | capture-20260502-143722-571 | Race | green | player | session-results; rows 57; valid 50; render True | model-v2-timing-fallback; fallback rows 63 | reliable:CarIdxF2Time; data True | Relative depends on model-v2 timing fallback rather than local proximity in this state. |
| endurance-24h-pit-or-garage | capture-20260502-143722-571 | Race | green | player | session-results; rows 57; valid 50; render True | model-v2-timing-fallback; fallback rows 63 | reliable:CarIdxF2Time; data True | Relative depends on model-v2 timing fallback rather than local proximity in this state. |

## Missing Targets

- `ai-race-green-player-focus` - AI race green with player focus
- `degraded-focus-unavailable` - Degraded state with missing focus car

## Source Captures

| Capture | Category | Frames | Dropped | Session Snapshots | Sample Stride |
| --- | --- | ---: | ---: | ---: | ---: |
| capture-20260511-001730-564 | ai-multisession-spectated | 35370 | 0 | 51 | 60 |
| capture-20260511-002956-343 | open-player-practice | 6375 | 0 | 11 | 60 |
| capture-20260426-130334-932 | endurance-4h-team-race | 1036026 | 0 | 2208 | 120 |
| capture-20260502-143722-571 | endurance-24h-fragment | 277680 | 0 | 750 | 120 |

# Live Telemetry State Corpus

Compact redacted states derived from local raw captures for Standings, Relative, and Gap To Leader source-selection work.

## States

| ID | Capture | Session | Phase | Clock | Focus | Standings | Relative | Gap | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ai-practice-no-valid-lap | capture-20260511-001730-564 | Practice | green | unavailable | player | none; rows 0; valid 0; render False | waiting; fallback rows 0 | unavailable:gap_signals_missing; data False | Standings should wait here because practice/qualifying/test requires a valid lap. |
| ai-qualifying-valid-lap-gated | capture-20260511-001730-564 | Lone Qualify | post-green | session remain 7.033s | non-player | session-results; rows 41; valid 38; render True | model-v2-timing-fallback; fallback rows 60 | reliable:position; data True | Practice/qualifying standings can render because selected scoring rows include valid lap times. Relative depends on model-v2 timing fallback rather than local proximity in this state. |
| ai-race-pre-green | capture-20260511-001730-564 | Race | pre-green | pre-green countdown 299.067s | non-player | starting-grid; rows 41; valid 38; render True | model-v2-timing-fallback; fallback rows 63 | unavailable:gap_signals_missing; data False | AI race state does not have usable local-player context; Standings/Relative/Gap must use focus/timing/scoring arrays. Relative depends on model-v2 timing fallback rather than local proximity in this state. Race pre-green may expose grid/scoring before live race gaps are meaningful. Race pre-green countdown is exposed through positive SessionTimeRemain in this SessionState phase. SessionState is a phase within the active SessionNum, not practice/qualifying/race session selection. |
| endurance-4h-race-pre-countdown | capture-20260426-130334-932 | Race | pre-green | pre-green countdown 59.767s | player | starting-grid; rows 60; valid 41; render True | live-proximity; fallback rows 63 | unavailable:gap_signals_missing; data False | Race pre-green may expose grid/scoring before live race gaps are meaningful. Race pre-green countdown is exposed through positive SessionTimeRemain in this SessionState phase. SessionState is a phase within the active SessionNum, not practice/qualifying/race session selection. |
| endurance-4h-race-pre-grid-no-countdown | capture-20260426-130334-932 | Race | pre-green | pre-green-no-countdown | player | starting-grid; rows 60; valid 41; render True | live-proximity; fallback rows 63 | unavailable:gap_signals_missing; data False | Race pre-green may expose grid/scoring before live race gaps are meaningful. SessionTimeRemain can return -1 during later grid/pace pre-green phases; do not infer a countdown from SessionTimeTotal minus SessionTime. SessionState is a phase within the active SessionNum, not practice/qualifying/race session selection. |
| ai-race-green-non-player-focus | capture-20260511-001730-564 | Race | green | session remain 7102.9s | non-player | session-results; rows 40; valid 40; render True | model-v2-timing-fallback; fallback rows 63 | reliable:CarIdxF2Time; data True | AI race state does not have usable local-player context; Standings/Relative/Gap must use focus/timing/scoring arrays. Relative depends on model-v2 timing fallback rather than local proximity in this state. |
| open-practice-non-player-focus | capture-20260511-002956-343 | Practice | green | session remain 4922.083s | non-player | session-results; rows 14; valid 14; render True | model-v2-timing-fallback; fallback rows 63 | reliable:CarIdxF2Time; data True | Practice/qualifying standings can render because selected scoring rows include valid lap times. Relative depends on model-v2 timing fallback rather than local proximity in this state. |
| open-practice-player-focus | capture-20260511-002956-343 | Practice | green | unavailable | player | none; rows 0; valid 0; render False | waiting; fallback rows 0 | unavailable:gap_signals_missing; data False | Standings should wait here because practice/qualifying/test requires a valid lap. |
| endurance-4h-race-running | capture-20260426-130334-932 | Race | green | session remain 13665.55s | player | session-results; rows 58; valid 55; render True | live-proximity; fallback rows 63 | reliable:CarIdxF2Time; data True |  |
| endurance-4h-pit-or-garage | capture-20260426-130334-932 | Race | green | session remain 11039.55s | player | session-results; rows 58; valid 55; render True | model-v2-timing-fallback; fallback rows 63 | reliable:CarIdxF2Time; data True | Relative depends on model-v2 timing fallback rather than local proximity in this state. |
| endurance-24h-race-running | capture-20260502-143722-571 | Race | green | session remain 29629.081s | player | session-results; rows 57; valid 50; render True | model-v2-timing-fallback; fallback rows 63 | reliable:CarIdxF2Time; data True | Relative depends on model-v2 timing fallback rather than local proximity in this state. |
| endurance-24h-pit-or-garage | capture-20260502-143722-571 | Race | green | session remain 26655.997s | player | session-results; rows 57; valid 50; render True | model-v2-timing-fallback; fallback rows 63 | reliable:CarIdxF2Time; data True | Relative depends on model-v2 timing fallback rather than local proximity in this state. |

## Missing Targets

- `ai-race-green-player-focus` - AI race green with player focus
- `degraded-focus-unavailable` - Degraded state with missing focus car

## Source Captures

| Capture | Category | Frames | Dropped | Session Snapshots | Sample Stride |
| --- | --- | ---: | ---: | ---: | ---: |
| capture-20260511-001730-564 | ai-multisession-spectated | 35370 | 0 | 51 | 60 |
| capture-20260511-002956-343 | open-player-practice | 6375 | 0 | 11 | 60 |
| capture-20260426-130334-932 | endurance-4h-team-race | 1036026 | 0 | 2208 | 60 |
| capture-20260502-143722-571 | endurance-24h-fragment | 277680 | 0 | 750 | 60 |

# SessionState Signal Availability

This is a compact comparison of scoring, position, gap, interval, and result
signals by `SessionState`, sampled from local raw captures at roughly 1 Hz
(`telemetry.bin` frame stride 60). It is intentionally summarized so the raw
captures stay local.

Sources:

- `capture-20260512-145607-242` - uploaded May 12, 2026 race-start capture.
- `capture-20260426-130334-932` - local four-hour endurance capture.
- `capture-20260502-143722-571` - local 24-hour endurance fragment.
- AI notes are from `live-telemetry-state-corpus.json` because the raw AI
  capture is not present in this checkout.

## Race Session Findings

`SessionNum` selects the race/practice/qualifying session. `SessionState`
describes the phase within that selected session. For race overlays, treat
states below `4` as pre-green even when `SessionNum` already points at a race.

| SessionState | Observed phase | Results / grid | Position signals | Gap / interval signals | Notes |
| --- | --- | --- | --- | --- | --- |
| `1` in race | pre-green countdown / early grid | Starting grid available from `QualifyResultsInfo`; race `ResultsPositions` may already exist in some sessions but should not drive live ordering yet. | `CarIdxPosition`, `CarIdxClassPosition`, `PlayerCarPosition`, and `PlayerCarClassPosition` were absent in sampled 4h race-start frames. | `CarIdxF2Time` was non-negative but not positive, so gap/interval values were not usable. `CarIdxEstTime` could be positive for many cars, but this is track-position estimation, not a race gap. | Positive `SessionTimeRemain` can expose the pre-race countdown in this phase. |
| `2` in race | pre-green grid/transition | Starting grid still available. | Position fields remained absent in sampled 4h frames. | No positive `CarIdxF2Time`; gap/interval unavailable. | `SessionTimeRemain` returned `-1.0` in sampled 4h frames. |
| `3` in race | pre-green grid/pace | Starting grid available in both uploaded and 4h captures. Uploaded capture had 40 grid rows; 4h had 60. | Position fields remained absent in sampled uploaded and 4h frames. | No positive `CarIdxF2Time`; official race gap/interval unavailable. `CarIdxEstTime` was positive for many cars. | This is enough to render ordered standings rows. Relative can use estimated-time deltas for pre-green/tow relative rows, but Standings keeps `GAP`/`INT` conservative until official timing coverage is meaningful. |
| `4` in race, uploaded capture | green / first minutes | Current race `ResultsPositions` appeared after green, about 40 rows. | About 40 `CarIdxPosition`/`CarIdxClassPosition` rows appeared; player position was present in most sampled frames. | `CarIdxF2Time` stayed non-positive for all sampled cars, so gap/interval remained unavailable even after green. | This matches the observed “positions update but GAP/INT do not” behavior for the uploaded capture. |
| `4` in race, 4h/24h captures | green / running race | Current race `ResultsPositions` available, 58 rows in 4h and 57 in 24h. | Position/class-position rows were broadly available, about 58 in 4h and 54 in 24h. | Positive `CarIdxF2Time` was broadly available, about 55 rows in 4h and 52 in 24h; focus gap/interval candidates were usable in about 97% and 95% of sampled frames. | This is the normal live-race case for Standings, Relative timing fallback, and Gap To Leader. |
| `5` in race | post-checkered / post-green | Current race `ResultsPositions` remained available in the 4h capture. | Position/class-position rows remained available. | Positive `CarIdxF2Time` remained available and gap/interval candidates were usable in sampled frames. | Good for final standings/history, but live projection should stop advancing. |

## Practice / Qualifying Findings

Practice and qualifying use `SessionNum`/session YAML type to decide their
behavior, not the numeric `SessionState` alone.

| SessionState | Session type | Results | Position / F2 | Standings implication |
| --- | --- | --- | --- | --- |
| `4` | practice | 4h post-hoc session rows existed, but sampled rows had no valid lap times for the selected practice session. | Position and positive F2 were absent in sampled frames; `CarIdxEstTime` had partial coverage. | Standings should wait for valid best/last lap evidence before rendering practice/test tables. |
| `4` | qualifying | Results rows and valid lap rows were available in the 4h qualifying segment. | Position/F2 coverage was partial while cars completed laps. | Standings can render once valid lap evidence exists, but gap/interval coverage may be sparse. |
| `6` | qualifying post-green | Results rows and valid lap rows remained available. | Position rows remained mostly available, but focus F2 was not positive in sampled frames. | Final qualifying standings can render from results even when live interval data is unavailable. |

## AI / Spectated Race Findings

The redacted AI corpus shows the same race-phase shape with a different local
context:

- AI race pre-green (`SessionNum = 2`, `SessionState = 1`) exposed starting-grid
  rows and a positive pre-race countdown, but live position/F2 gaps were not
  usable.
- AI race green (`SessionState = 4`) exposed session results, about 40 position
  rows, and about 39 positive F2 rows. Gap/interval data was usable from timing
  arrays even though the local-player radar/proximity context was unavailable.

## Overlay Implications

- `SessionState < 4` should allow grid/scoring rows, but official live race
  gaps and Gap To Leader should stay gated off.
- Race `SessionState == 3` can support Relative timing fallback from positive
  `CarIdxEstTime` plus valid `CarIdxLapDistPct`, including local tow cases
  where iRacing still displays estimated relative gaps. Standings should keep
  starting-grid row order and leave estimated `GAP`/`INT` cells empty for now.
- `SessionState == 4` is necessary but not sufficient for gap/interval display.
  Consumers still need positive F2 or another explicit gap source for the focus
  car and comparison rows.
- `CarIdxEstTime` can be populated before green and while F2 is zero. It is
  useful for track-position estimation and the state-3 Relative fallback rule
  above, but should not be displayed as an official race class gap.
- Current race `ResultsPositions` may be missing before green, as in the
  uploaded capture, or present but stale/not meaningful, as in the 4h capture.
  Race pre-green standings should prefer the starting grid until live race
  position/class-position/progress coverage is meaningful.

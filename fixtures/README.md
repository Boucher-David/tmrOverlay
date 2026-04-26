# Fixtures

This folder contains small, git-tracked sample data for tests, parser development, and overlay prototyping.

Raw captures stay under `captures/` and are intentionally ignored because they can be multi-GB. Fixtures should be synthetic or sanitized, compact enough to review in git, and stable enough to act as contracts for the fields the app expects.

## Current Fixtures

- `iracing/session-info/synthetic-endurance-team-race.yaml`
  - Synthetic iRacing endurance session-info shape.
  - Includes weekend, session, driver, result, setup, split, radio, and camera data.
  - Adds a fixture-only `SyntheticTelemetryFrames` block because live fuel/input values come from `telemetry.bin`, not from iRacing session YAML.

## Feature Ideas

- Intelligent capture of historical session data to help model future calculations
- Localhost versions of the overlay so streamers can use it
- Weather display?
- Some kind of graph showing gaps to class leader so users/spotters can see how race gap is closing over multiple stints
- Suspected damage analysis: track incident-count increases during a stint as candidate damage events, confirm later with `PitRepairLeft`, `PitOptRepairLeft`, or fast-repair counters when available, and estimate lap-time impact by comparing post-event clean laps against expected pace while controlling for fuel, tire age/compound, wetness, traffic, pit-out laps, and driver. Label this as suspected damage unless repair telemetry confirms it. Surface this primarily in Gap To Leader with timeline markers, shaded suspected-damage intervals, and pace-loss estimates such as `+1.4s/lap`; feed the simplified consequence into Fuel Calculator for repair/unscheduled-stop strategy updates; keep Status limited to compact warnings and leave Car Radar mostly unaffected.

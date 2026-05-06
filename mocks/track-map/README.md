# Track Map Mocks

Generated mac-harness previews for track-map sector highlight states.

- `track-map-sector-states.png` is the contact sheet for normal, sector personal-best, full-lap best, following-sector reset, and mixed live-sector states.
- `states/normal.png` shows the continuous white base map with live markers.
- `states/sector-personal-best.png` shows green personal-best sector overlays.
- `states/session-best-lap.png` shows the temporary full-lap purple highlight.
- `states/following-sector-one.png` shows the next-lap reset where only the completed first sector stays highlighted.
- `states/mixed-live-sectors.png` shows simultaneous green and purple sector statuses.

These PNGs are deterministic screenshot validation artifacts. They prove the sector-color contract is present in generated screenshots, but visual QA of production geometry still belongs to the Windows screenshot artifact and local/live map demos.

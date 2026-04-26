# Session History

This folder stores compact historical summaries that the app can use before a live session has enough data for a stronger current-session fuel model.

Tracked baseline data lives under `history/baseline/`. User-generated data is written to the app-owned local data folder by default.

Baseline hierarchy:

```text
history/
  baseline/
    cars/
      {car-key}/
        tracks/
          {track-key}/
            sessions/
              {session-key}/
                aggregate.json
                summaries/
                  {capture-id}.json
```

User history hierarchy:

```text
%LOCALAPPDATA%/TmrOverlay/history/user/
  cars/
    {car-key}/
      tracks/
        {track-key}/
          sessions/
            {session-key}/
              aggregate.json
              summaries/
                {capture-id}.json
```

Raw telemetry captures are stored under `%LOCALAPPDATA%/TmrOverlay/captures/` by default. Files in `history/baseline/` should stay small enough to ship with the application as baseline knowledge, then be supplemented locally as a user records more of their own sessions.

The first summary format is intentionally conservative. Short captures, offline tests, and sessions with no completed laps are still stored, but are marked with low confidence and do not contribute to the baseline aggregate until enough clean distance is available.

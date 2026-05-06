Bundled track-map JSON assets generated from vetted IBT telemetry live here.

Current bundled assets use track-map schema v2: generated racing-line geometry plus sector boundaries from `SplitTimeInfo.Sectors` when the source IBT/session info provides them. Run `TmrOverlay.TrackMapGenerator` against the ignored `captures/IBT` corpus and review confidence/sector output before committing generated maps. Do not commit source `.ibt` files.

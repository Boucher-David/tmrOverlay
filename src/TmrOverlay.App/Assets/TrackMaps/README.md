Bundled track-map JSON assets generated from vetted IBT telemetry live here.

Current bundled assets use track-map schema v2: generated racing-line geometry plus sector boundaries from `SplitTimeInfo.Sectors` when the source IBT/session info provides them. Run `TmrOverlay.TrackMapGenerator` against the ignored `captures/IBT` corpus and review confidence/sector output before committing generated maps. Do not commit source `.ibt` files.

Some files intentionally look duplicated by venue. Runtime lookup uses the exact `TrackMapIdentity.Key`, which includes track id, layout/config, length, and iRacing `TrackVersion`. Keep true layout variants, such as road/oval or full/north configurations. Also keep same-layout version variants unless `TrackMapStore` grows an explicit compatible-version fallback; deleting an older version can make older sessions and captures fall back to the circle placeholder even when the newer geometry looks equivalent.

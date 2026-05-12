# SDK Field Availability Corpus

Compact redacted availability map for SDK variables observed in local raw captures.

- Sources: 2
- SDK fields: 334
- Raw telemetry frames and private session-info identity values are not included.
- `sdkDeclaredShape` records SDK/storage shape maximums; observed min/max values come from sampled captures.

## Sources

| Capture | Category | Frames | Schema Fields | Sampled Frames | Identity Shape |
| --- | --- | ---: | ---: | ---: | --- |
| capture-20260426-130334-932 | endurance-4h-team-race | 1036026 | 334 | 1730 | drivers 61; user names 61; team names 61; blank class names 1 |
| capture-20260502-143722-571 | endurance-24h-fragment | 277680 | 334 | 466 | drivers 60; user names 60; team names 60; blank class names 1 |

## Category Counts

- `driver-change`: 2
- `engine`: 19
- `fuel`: 9
- `input`: 32
- `misc`: 74
- `per-car`: 30
- `pit-service`: 98
- `race-control`: 12
- `radio-camera`: 7
- `scoring`: 7
- `session`: 34
- `vehicle-dynamics`: 46
- `weather`: 13

## Field Index

| Field | Type | Count | Max Index | Bytes | Unit | Categories | Present In |
| --- | --- | ---: | ---: | ---: | --- | --- | --- |
| AirDensity | irFloat | 1 | 0 | 4 | kg/m^3 | misc | 2 |
| AirPressure | irFloat | 1 | 0 | 4 | Pa | misc | 2 |
| AirTemp | irFloat | 1 | 0 | 4 | C | weather | 2 |
| Brake | irFloat | 1 | 0 | 4 | % | input | 2 |
| BrakeABSactive | irBool | 1 | 0 | 1 |  | input | 2 |
| BrakeRaw | irFloat | 1 | 0 | 4 | % | input | 2 |
| CamCameraNumber | irInt | 1 | 0 | 4 |  | radio-camera | 2 |
| CamCameraState | irBitField | 1 | 0 | 4 | irsdk_CameraState | radio-camera | 2 |
| CamCarIdx | irInt | 1 | 0 | 4 |  | per-car, radio-camera | 2 |
| CamGroupNumber | irInt | 1 | 0 | 4 |  | radio-camera | 2 |
| CarDistAhead | irFloat | 1 | 0 | 4 | m | misc | 2 |
| CarDistBehind | irFloat | 1 | 0 | 4 | m | misc | 2 |
| CarIdxBestLapNum | irInt | 64 | 63 | 256 |  | per-car | 2 |
| CarIdxBestLapTime | irFloat | 64 | 63 | 256 | s | per-car | 2 |
| CarIdxClass | irInt | 64 | 63 | 256 |  | per-car | 2 |
| CarIdxClassPosition | irInt | 64 | 63 | 256 |  | scoring, per-car | 2 |
| CarIdxEstTime | irFloat | 64 | 63 | 256 | s | per-car | 2 |
| CarIdxF2Time | irFloat | 64 | 63 | 256 | s | scoring, per-car | 2 |
| CarIdxFastRepairsUsed | irInt | 64 | 63 | 256 |  | per-car, pit-service | 2 |
| CarIdxGear | irInt | 64 | 63 | 256 |  | per-car, input | 2 |
| CarIdxLap | irInt | 64 | 63 | 256 |  | per-car | 2 |
| CarIdxLapCompleted | irInt | 64 | 63 | 256 |  | per-car | 2 |
| CarIdxLapDistPct | irFloat | 64 | 63 | 256 | % | per-car | 2 |
| CarIdxLastLapTime | irFloat | 64 | 63 | 256 | s | scoring, per-car | 2 |
| CarIdxOnPitRoad | irBool | 64 | 63 | 64 |  | per-car, pit-service | 2 |
| CarIdxP2P_Count | irInt | 64 | 63 | 256 |  | per-car | 2 |
| CarIdxP2P_Status | irBool | 64 | 63 | 64 |  | per-car | 2 |
| CarIdxPaceFlags | irBitField | 64 | 63 | 256 | irsdk_PaceFlags | per-car, race-control | 2 |
| CarIdxPaceLine | irInt | 64 | 63 | 256 |  | per-car, race-control | 2 |
| CarIdxPaceRow | irInt | 64 | 63 | 256 |  | per-car, race-control | 2 |
| CarIdxPosition | irInt | 64 | 63 | 256 |  | scoring, per-car | 2 |
| CarIdxQualTireCompound | irInt | 64 | 63 | 256 |  | per-car, pit-service, vehicle-dynamics | 2 |
| CarIdxQualTireCompoundLocked | irBool | 64 | 63 | 64 |  | per-car, pit-service, vehicle-dynamics | 2 |
| CarIdxRPM | irFloat | 64 | 63 | 256 | revs/min | per-car, engine | 2 |
| CarIdxSessionFlags | irBitField | 64 | 63 | 256 | irsdk_Flags | session, per-car, race-control | 2 |
| CarIdxSteer | irFloat | 64 | 63 | 256 | rad | per-car, input | 2 |
| CarIdxTireCompound | irInt | 64 | 63 | 256 |  | per-car, pit-service | 2 |
| CarIdxTrackSurface | irInt | 64 | 63 | 256 | irsdk_TrkLoc | per-car | 2 |
| CarIdxTrackSurfaceMaterial | irInt | 64 | 63 | 256 | irsdk_TrkSurf | per-car | 2 |
| CarLeftRight | irInt | 1 | 0 | 4 | irsdk_CarLeftRight | misc | 2 |
| ChanAvgLatency | irFloat | 1 | 0 | 4 | s | vehicle-dynamics | 2 |
| ChanClockSkew | irFloat | 1 | 0 | 4 | s | misc | 2 |
| ChanLatency | irFloat | 1 | 0 | 4 | s | vehicle-dynamics | 2 |
| ChanPartnerQuality | irFloat | 1 | 0 | 4 | % | misc | 2 |
| ChanQuality | irFloat | 1 | 0 | 4 | % | misc | 2 |
| Clutch | irFloat | 1 | 0 | 4 | % | input | 2 |
| ClutchRaw | irFloat | 1 | 0 | 4 | % | input | 2 |
| CpuUsageBG | irFloat | 1 | 0 | 4 | % | misc | 2 |
| CpuUsageFG | irFloat | 1 | 0 | 4 | % | misc | 2 |
| DCDriversSoFar | irInt | 1 | 0 | 4 |  | driver-change | 2 |
| DCLapStatus | irInt | 1 | 0 | 4 |  | driver-change | 2 |
| DisplayUnits | irInt | 1 | 0 | 4 |  | misc | 2 |
| DriverMarker | irBool | 1 | 0 | 1 |  | race-control | 2 |
| Engine0_RPM | irFloat | 1 | 0 | 4 | revs/min | engine | 2 |
| EngineWarnings | irBitField | 1 | 0 | 4 | irsdk_EngineWarnings | engine | 2 |
| EnterExitReset | irInt | 1 | 0 | 4 |  | misc | 2 |
| FastRepairAvailable | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| FastRepairUsed | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| FogLevel | irFloat | 1 | 0 | 4 | % | misc | 2 |
| FrameRate | irFloat | 1 | 0 | 4 | fps | misc | 2 |
| FrontTireSetsAvailable | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| FrontTireSetsUsed | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| FuelLevel | irFloat | 1 | 0 | 4 | l | fuel | 2 |
| FuelLevelPct | irFloat | 1 | 0 | 4 | % | fuel | 2 |
| FuelPress | irFloat | 1 | 0 | 4 | bar | fuel, engine | 2 |
| FuelUsePerHour | irFloat | 1 | 0 | 4 | kg/h | fuel, engine | 2 |
| Gear | irInt | 1 | 0 | 4 |  | input | 2 |
| GpuUsage | irFloat | 1 | 0 | 4 | % | misc | 2 |
| HandbrakeRaw | irFloat | 1 | 0 | 4 | % | input | 2 |
| IsDiskLoggingActive | irBool | 1 | 0 | 1 |  | misc | 2 |
| IsDiskLoggingEnabled | irBool | 1 | 0 | 1 |  | misc | 2 |
| IsGarageVisible | irBool | 1 | 0 | 1 |  | misc | 2 |
| IsInGarage | irBool | 1 | 0 | 1 |  | misc | 2 |
| IsOnTrack | irBool | 1 | 0 | 1 |  | misc | 2 |
| IsOnTrackCar | irBool | 1 | 0 | 1 |  | misc | 2 |
| IsReplayPlaying | irBool | 1 | 0 | 1 |  | session | 2 |
| LFTiresAvailable | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| LFTiresUsed | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| LFbrakeLinePress | irFloat | 1 | 0 | 4 | bar | input | 2 |
| LFcoldPressure | irFloat | 1 | 0 | 4 | kPa | pit-service | 2 |
| LFodometer | irFloat | 1 | 0 | 4 | m | pit-service | 2 |
| LFshockDefl | irFloat | 1 | 0 | 4 | m | misc | 2 |
| LFshockDefl_ST | irFloat | 6 | 5 | 24 | m | misc | 2 |
| LFshockVel | irFloat | 1 | 0 | 4 | m/s | vehicle-dynamics | 2 |
| LFshockVel_ST | irFloat | 6 | 5 | 24 | m/s | vehicle-dynamics | 2 |
| LFtempCL | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| LFtempCM | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| LFtempCR | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| LFwearL | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| LFwearM | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| LFwearR | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| LRTiresAvailable | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| LRTiresUsed | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| LRbrakeLinePress | irFloat | 1 | 0 | 4 | bar | input | 2 |
| LRcoldPressure | irFloat | 1 | 0 | 4 | kPa | pit-service | 2 |
| LRodometer | irFloat | 1 | 0 | 4 | m | pit-service | 2 |
| LRshockDefl | irFloat | 1 | 0 | 4 | m | misc | 2 |
| LRshockDefl_ST | irFloat | 6 | 5 | 24 | m | misc | 2 |
| LRshockVel | irFloat | 1 | 0 | 4 | m/s | vehicle-dynamics | 2 |
| LRshockVel_ST | irFloat | 6 | 5 | 24 | m/s | vehicle-dynamics | 2 |
| LRtempCL | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| LRtempCM | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| LRtempCR | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| LRwearL | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| LRwearM | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| LRwearR | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| Lap | irInt | 1 | 0 | 4 |  | misc | 2 |
| LapBestLap | irInt | 1 | 0 | 4 |  | misc | 2 |
| LapBestLapTime | irFloat | 1 | 0 | 4 | s | misc | 2 |
| LapBestNLapLap | irInt | 1 | 0 | 4 |  | misc | 2 |
| LapBestNLapTime | irFloat | 1 | 0 | 4 | s | misc | 2 |
| LapCompleted | irInt | 1 | 0 | 4 |  | misc | 2 |
| LapCurrentLapTime | irFloat | 1 | 0 | 4 | s | misc | 2 |
| LapDeltaToBestLap | irFloat | 1 | 0 | 4 | s | misc | 2 |
| LapDeltaToBestLap_DD | irFloat | 1 | 0 | 4 | s/s | misc | 2 |
| LapDeltaToBestLap_OK | irBool | 1 | 0 | 1 |  | misc | 2 |
| LapDeltaToOptimalLap | irFloat | 1 | 0 | 4 | s | misc | 2 |
| LapDeltaToOptimalLap_DD | irFloat | 1 | 0 | 4 | s/s | misc | 2 |
| LapDeltaToOptimalLap_OK | irBool | 1 | 0 | 1 |  | misc | 2 |
| LapDeltaToSessionBestLap | irFloat | 1 | 0 | 4 | s | session | 2 |
| LapDeltaToSessionBestLap_DD | irFloat | 1 | 0 | 4 | s/s | session | 2 |
| LapDeltaToSessionBestLap_OK | irBool | 1 | 0 | 1 |  | session | 2 |
| LapDeltaToSessionLastlLap | irFloat | 1 | 0 | 4 | s | session | 2 |
| LapDeltaToSessionLastlLap_DD | irFloat | 1 | 0 | 4 | s/s | session | 2 |
| LapDeltaToSessionLastlLap_OK | irBool | 1 | 0 | 1 |  | session | 2 |
| LapDeltaToSessionOptimalLap | irFloat | 1 | 0 | 4 | s | session | 2 |
| LapDeltaToSessionOptimalLap_DD | irFloat | 1 | 0 | 4 | s/s | session | 2 |
| LapDeltaToSessionOptimalLap_OK | irBool | 1 | 0 | 1 |  | session | 2 |
| LapDist | irFloat | 1 | 0 | 4 | m | misc | 2 |
| LapDistPct | irFloat | 1 | 0 | 4 | % | misc | 2 |
| LapLasNLapSeq | irInt | 1 | 0 | 4 |  | misc | 2 |
| LapLastLapTime | irFloat | 1 | 0 | 4 | s | scoring | 2 |
| LapLastNLapTime | irFloat | 1 | 0 | 4 | s | misc | 2 |
| LatAccel | irFloat | 1 | 0 | 4 | m/s^2 | vehicle-dynamics | 2 |
| LatAccel_ST | irFloat | 6 | 5 | 24 | m/s^2 | vehicle-dynamics | 2 |
| LeftTireSetsAvailable | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| LeftTireSetsUsed | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| LoadNumTextures | irBool | 1 | 0 | 1 |  | misc | 2 |
| LongAccel | irFloat | 1 | 0 | 4 | m/s^2 | vehicle-dynamics | 2 |
| LongAccel_ST | irFloat | 6 | 5 | 24 | m/s^2 | vehicle-dynamics | 2 |
| ManifoldPress | irFloat | 1 | 0 | 4 | bar | engine | 2 |
| ManualBoost | irBool | 1 | 0 | 1 |  | misc | 2 |
| ManualNoBoost | irBool | 1 | 0 | 1 |  | misc | 2 |
| MemPageFaultSec | irFloat | 1 | 0 | 4 |  | misc | 2 |
| MemSoftPageFaultSec | irFloat | 1 | 0 | 4 |  | misc | 2 |
| OilLevel | irFloat | 1 | 0 | 4 | l | engine | 2 |
| OilPress | irFloat | 1 | 0 | 4 | bar | engine | 2 |
| OilTemp | irFloat | 1 | 0 | 4 | C | engine | 2 |
| OkToReloadTextures | irBool | 1 | 0 | 1 |  | misc | 2 |
| OnPitRoad | irBool | 1 | 0 | 1 |  | pit-service | 2 |
| P2P_Count | irInt | 1 | 0 | 4 |  | misc | 2 |
| P2P_Status | irBool | 1 | 0 | 1 |  | misc | 2 |
| PaceMode | irInt | 1 | 0 | 4 | irsdk_PaceMode | race-control | 2 |
| PitOptRepairLeft | irFloat | 1 | 0 | 4 | s | pit-service | 2 |
| PitRepairLeft | irFloat | 1 | 0 | 4 | s | pit-service | 2 |
| PitSvFlags | irBitField | 1 | 0 | 4 | irsdk_PitSvFlags | race-control, pit-service | 2 |
| PitSvFuel | irFloat | 1 | 0 | 4 | l or kWh | pit-service, fuel | 2 |
| PitSvLFP | irFloat | 1 | 0 | 4 | kPa | pit-service | 2 |
| PitSvLRP | irFloat | 1 | 0 | 4 | kPa | pit-service | 2 |
| PitSvRFP | irFloat | 1 | 0 | 4 | kPa | pit-service | 2 |
| PitSvRRP | irFloat | 1 | 0 | 4 | kPa | pit-service | 2 |
| PitSvTireCompound | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| Pitch | irFloat | 1 | 0 | 4 | rad | pit-service, vehicle-dynamics | 2 |
| PitchRate | irFloat | 1 | 0 | 4 | rad/s | pit-service, vehicle-dynamics | 2 |
| PitchRate_ST | irFloat | 6 | 5 | 24 | rad/s | pit-service, vehicle-dynamics | 2 |
| PitsOpen | irBool | 1 | 0 | 1 |  | pit-service | 2 |
| PitstopActive | irBool | 1 | 0 | 1 |  | pit-service | 2 |
| PlayerCarClass | irInt | 1 | 0 | 4 |  | misc | 2 |
| PlayerCarClassPosition | irInt | 1 | 0 | 4 |  | scoring | 2 |
| PlayerCarDriverIncidentCount | irInt | 1 | 0 | 4 |  | session | 2 |
| PlayerCarDryTireSetLimit | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| PlayerCarIdx | irInt | 1 | 0 | 4 |  | per-car | 2 |
| PlayerCarInPitStall | irBool | 1 | 0 | 1 |  | pit-service | 2 |
| PlayerCarMyIncidentCount | irInt | 1 | 0 | 4 |  | session | 2 |
| PlayerCarPitSvStatus | irInt | 1 | 0 | 4 | irsdk_PitSvStatus | pit-service | 2 |
| PlayerCarPosition | irInt | 1 | 0 | 4 |  | scoring | 2 |
| PlayerCarPowerAdjust | irFloat | 1 | 0 | 4 | % | misc | 2 |
| PlayerCarSLBlinkRPM | irFloat | 1 | 0 | 4 | revs/min | engine | 2 |
| PlayerCarSLFirstRPM | irFloat | 1 | 0 | 4 | revs/min | engine | 2 |
| PlayerCarSLLastRPM | irFloat | 1 | 0 | 4 | revs/min | engine | 2 |
| PlayerCarSLShiftRPM | irFloat | 1 | 0 | 4 | revs/min | engine | 2 |
| PlayerCarTeamIncidentCount | irInt | 1 | 0 | 4 |  | session | 2 |
| PlayerCarTowTime | irFloat | 1 | 0 | 4 | s | misc | 2 |
| PlayerCarWeightPenalty | irFloat | 1 | 0 | 4 | kg | vehicle-dynamics | 2 |
| PlayerFastRepairsUsed | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| PlayerIncidents | irInt | 1 | 0 | 4 | irsdk_IncidentFlags | misc | 2 |
| PlayerTireCompound | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| PlayerTrackSurface | irInt | 1 | 0 | 4 | irsdk_TrkLoc | misc | 2 |
| PlayerTrackSurfaceMaterial | irInt | 1 | 0 | 4 | irsdk_TrkSurf | misc | 2 |
| Precipitation | irFloat | 1 | 0 | 4 | % | pit-service, weather | 2 |
| PushToPass | irBool | 1 | 0 | 1 |  | misc | 2 |
| PushToTalk | irBool | 1 | 0 | 1 |  | misc | 2 |
| RFTiresAvailable | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| RFTiresUsed | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| RFbrakeLinePress | irFloat | 1 | 0 | 4 | bar | input | 2 |
| RFcoldPressure | irFloat | 1 | 0 | 4 | kPa | pit-service | 2 |
| RFodometer | irFloat | 1 | 0 | 4 | m | pit-service | 2 |
| RFshockDefl | irFloat | 1 | 0 | 4 | m | misc | 2 |
| RFshockDefl_ST | irFloat | 6 | 5 | 24 | m | misc | 2 |
| RFshockVel | irFloat | 1 | 0 | 4 | m/s | vehicle-dynamics | 2 |
| RFshockVel_ST | irFloat | 6 | 5 | 24 | m/s | vehicle-dynamics | 2 |
| RFtempCL | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| RFtempCM | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| RFtempCR | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| RFwearL | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| RFwearM | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| RFwearR | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| RPM | irFloat | 1 | 0 | 4 | revs/min | engine | 2 |
| RRTiresAvailable | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| RRTiresUsed | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| RRbrakeLinePress | irFloat | 1 | 0 | 4 | bar | input | 2 |
| RRcoldPressure | irFloat | 1 | 0 | 4 | kPa | pit-service | 2 |
| RRodometer | irFloat | 1 | 0 | 4 | m | pit-service | 2 |
| RRshockDefl | irFloat | 1 | 0 | 4 | m | misc | 2 |
| RRshockDefl_ST | irFloat | 6 | 5 | 24 | m | misc | 2 |
| RRshockVel | irFloat | 1 | 0 | 4 | m/s | vehicle-dynamics | 2 |
| RRshockVel_ST | irFloat | 6 | 5 | 24 | m/s | vehicle-dynamics | 2 |
| RRtempCL | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| RRtempCM | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| RRtempCR | irFloat | 1 | 0 | 4 | C | pit-service | 2 |
| RRwearL | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| RRwearM | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| RRwearR | irFloat | 1 | 0 | 4 | % | pit-service | 2 |
| RaceLaps | irInt | 1 | 0 | 4 |  | misc | 2 |
| RadioTransmitCarIdx | irInt | 1 | 0 | 4 |  | per-car, radio-camera | 2 |
| RadioTransmitFrequencyIdx | irInt | 1 | 0 | 4 |  | radio-camera | 2 |
| RadioTransmitRadioIdx | irInt | 1 | 0 | 4 |  | radio-camera | 2 |
| RearTireSetsAvailable | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| RearTireSetsUsed | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| RelativeHumidity | irFloat | 1 | 0 | 4 | % | vehicle-dynamics | 2 |
| ReplayFrameNum | irInt | 1 | 0 | 4 |  | session | 2 |
| ReplayFrameNumEnd | irInt | 1 | 0 | 4 |  | session | 2 |
| ReplayPlaySlowMotion | irBool | 1 | 0 | 1 |  | session | 2 |
| ReplayPlaySpeed | irInt | 1 | 0 | 4 |  | session, vehicle-dynamics | 2 |
| ReplaySessionNum | irInt | 1 | 0 | 4 |  | session | 2 |
| ReplaySessionTime | irDouble | 1 | 0 | 8 | s | session | 2 |
| RightTireSetsAvailable | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| RightTireSetsUsed | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| Roll | irFloat | 1 | 0 | 4 | rad | vehicle-dynamics | 2 |
| RollRate | irFloat | 1 | 0 | 4 | rad/s | vehicle-dynamics | 2 |
| RollRate_ST | irFloat | 6 | 5 | 24 | rad/s | vehicle-dynamics | 2 |
| SessionFlags | irBitField | 1 | 0 | 4 | irsdk_Flags | session, race-control | 2 |
| SessionJokerLapsRemain | irInt | 1 | 0 | 4 |  | session, race-control | 2 |
| SessionLapsRemain | irInt | 1 | 0 | 4 |  | session | 2 |
| SessionLapsRemainEx | irInt | 1 | 0 | 4 |  | session | 2 |
| SessionLapsTotal | irInt | 1 | 0 | 4 |  | session | 2 |
| SessionNum | irInt | 1 | 0 | 4 |  | session | 2 |
| SessionOnJokerLap | irBool | 1 | 0 | 1 |  | session, race-control | 2 |
| SessionState | irInt | 1 | 0 | 4 | irsdk_SessionState | session | 2 |
| SessionTick | irInt | 1 | 0 | 4 |  | session | 2 |
| SessionTime | irDouble | 1 | 0 | 8 | s | session | 2 |
| SessionTimeOfDay | irFloat | 1 | 0 | 4 | s | session | 2 |
| SessionTimeRemain | irDouble | 1 | 0 | 8 | s | session | 2 |
| SessionTimeTotal | irDouble | 1 | 0 | 8 | s | session | 2 |
| SessionUniqueID | irInt | 1 | 0 | 4 |  | session | 2 |
| ShiftGrindRPM | irFloat | 1 | 0 | 4 | RPM | engine | 2 |
| ShiftIndicatorPct | irFloat | 1 | 0 | 4 | % | engine | 2 |
| ShiftPowerPct | irFloat | 1 | 0 | 4 | % | input | 2 |
| Shifter | irInt | 1 | 0 | 4 |  | misc | 2 |
| Skies | irInt | 1 | 0 | 4 |  | misc | 2 |
| SolarAltitude | irFloat | 1 | 0 | 4 | rad | weather, vehicle-dynamics | 2 |
| SolarAzimuth | irFloat | 1 | 0 | 4 | rad | weather | 2 |
| Speed | irFloat | 1 | 0 | 4 | m/s | vehicle-dynamics | 2 |
| SteeringFFBEnabled | irBool | 1 | 0 | 1 |  | input | 2 |
| SteeringWheelAngle | irFloat | 1 | 0 | 4 | rad | input | 2 |
| SteeringWheelAngleMax | irFloat | 1 | 0 | 4 | rad | input | 2 |
| SteeringWheelLimiter | irFloat | 1 | 0 | 4 | % | input, vehicle-dynamics | 2 |
| SteeringWheelMaxForceNm | irFloat | 1 | 0 | 4 | N*m | input | 2 |
| SteeringWheelPctDamper | irFloat | 1 | 0 | 4 | % | input | 2 |
| SteeringWheelPctIntensity | irFloat | 1 | 0 | 4 | % | input | 2 |
| SteeringWheelPctSmoothing | irFloat | 1 | 0 | 4 | % | input | 2 |
| SteeringWheelPctTorque | irFloat | 1 | 0 | 4 | % | input | 2 |
| SteeringWheelPctTorqueSign | irFloat | 1 | 0 | 4 | % | input | 2 |
| SteeringWheelPctTorqueSignStops | irFloat | 1 | 0 | 4 | % | input | 2 |
| SteeringWheelPeakForceNm | irFloat | 1 | 0 | 4 | N*m | input | 2 |
| SteeringWheelTorque | irFloat | 1 | 0 | 4 | N*m | input | 2 |
| SteeringWheelTorque_ST | irFloat | 6 | 5 | 24 | N*m | input | 2 |
| SteeringWheelUseLinear | irBool | 1 | 0 | 1 |  | input | 2 |
| Throttle | irFloat | 1 | 0 | 4 | % | input | 2 |
| ThrottleRaw | irFloat | 1 | 0 | 4 | % | input | 2 |
| TireLF_RumblePitch | irFloat | 1 | 0 | 4 | Hz | pit-service, vehicle-dynamics | 2 |
| TireLR_RumblePitch | irFloat | 1 | 0 | 4 | Hz | pit-service, vehicle-dynamics | 2 |
| TireRF_RumblePitch | irFloat | 1 | 0 | 4 | Hz | pit-service, vehicle-dynamics | 2 |
| TireRR_RumblePitch | irFloat | 1 | 0 | 4 | Hz | pit-service, vehicle-dynamics | 2 |
| TireSetsAvailable | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| TireSetsUsed | irInt | 1 | 0 | 4 |  | pit-service | 2 |
| TrackTemp | irFloat | 1 | 0 | 4 | C | weather | 2 |
| TrackTempCrew | irFloat | 1 | 0 | 4 | C | weather | 2 |
| TrackWetness | irInt | 1 | 0 | 4 | irsdk_TrackWetness | weather | 2 |
| VelocityX | irFloat | 1 | 0 | 4 | m/s | vehicle-dynamics | 2 |
| VelocityX_ST | irFloat | 6 | 5 | 24 | m/s at 360 Hz | vehicle-dynamics | 2 |
| VelocityY | irFloat | 1 | 0 | 4 | m/s | vehicle-dynamics | 2 |
| VelocityY_ST | irFloat | 6 | 5 | 24 | m/s at 360 Hz | vehicle-dynamics | 2 |
| VelocityZ | irFloat | 1 | 0 | 4 | m/s | vehicle-dynamics | 2 |
| VelocityZ_ST | irFloat | 6 | 5 | 24 | m/s at 360 Hz | vehicle-dynamics | 2 |
| VertAccel | irFloat | 1 | 0 | 4 | m/s^2 | vehicle-dynamics | 2 |
| VertAccel_ST | irFloat | 6 | 5 | 24 | m/s^2 | vehicle-dynamics | 2 |
| VidCapActive | irBool | 1 | 0 | 1 |  | misc | 2 |
| VidCapEnabled | irBool | 1 | 0 | 1 |  | misc | 2 |
| Voltage | irFloat | 1 | 0 | 4 | V | engine | 2 |
| WaterLevel | irFloat | 1 | 0 | 4 | l | engine | 2 |
| WaterTemp | irFloat | 1 | 0 | 4 | C | engine | 2 |
| WeatherDeclaredWet | irBool | 1 | 0 | 1 |  | pit-service, weather | 2 |
| WindDir | irFloat | 1 | 0 | 4 | rad | weather | 2 |
| WindVel | irFloat | 1 | 0 | 4 | m/s | weather, vehicle-dynamics | 2 |
| Yaw | irFloat | 1 | 0 | 4 | rad | vehicle-dynamics | 2 |
| YawNorth | irFloat | 1 | 0 | 4 | rad | vehicle-dynamics | 2 |
| YawRate | irFloat | 1 | 0 | 4 | rad/s | vehicle-dynamics | 2 |
| YawRate_ST | irFloat | 6 | 5 | 24 | rad/s | vehicle-dynamics | 2 |
| dcABS | irFloat | 1 | 0 | 4 |  | misc | 2 |
| dcABSToggle | irBool | 1 | 0 | 1 |  | misc | 2 |
| dcBrakeBias | irFloat | 1 | 0 | 4 |  | input | 2 |
| dcDashPage | irFloat | 1 | 0 | 4 |  | misc | 2 |
| dcHeadlightFlash | irBool | 1 | 0 | 1 |  | misc | 2 |
| dcPitSpeedLimiterToggle | irBool | 1 | 0 | 1 |  | pit-service, vehicle-dynamics | 2 |
| dcStarter | irBool | 1 | 0 | 1 |  | misc | 2 |
| dcToggleWindshieldWipers | irBool | 1 | 0 | 1 |  | weather | 2 |
| dcTractionControl | irFloat | 1 | 0 | 4 |  | misc | 2 |
| dcTractionControlToggle | irBool | 1 | 0 | 1 |  | misc | 2 |
| dcTriggerWindshieldWipers | irBool | 1 | 0 | 1 |  | weather | 2 |
| dpFastRepair | irFloat | 1 | 0 | 4 |  | pit-service | 2 |
| dpFuelAddKg | irFloat | 1 | 0 | 4 | kg | pit-service, fuel | 2 |
| dpFuelAutoFillActive | irFloat | 1 | 0 | 4 |  | race-control, pit-service, fuel | 2 |
| dpFuelAutoFillEnabled | irFloat | 1 | 0 | 4 |  | pit-service, fuel | 2 |
| dpFuelFill | irFloat | 1 | 0 | 4 |  | race-control, pit-service, fuel | 2 |
| dpLFTireChange | irFloat | 1 | 0 | 4 |  | pit-service | 2 |
| dpLFTireColdPress | irFloat | 1 | 0 | 4 | Pa | pit-service | 2 |
| dpLRTireChange | irFloat | 1 | 0 | 4 |  | pit-service | 2 |
| dpLRTireColdPress | irFloat | 1 | 0 | 4 | Pa | pit-service | 2 |
| dpRFTireChange | irFloat | 1 | 0 | 4 |  | pit-service | 2 |
| dpRFTireColdPress | irFloat | 1 | 0 | 4 | Pa | pit-service | 2 |
| dpRRTireChange | irFloat | 1 | 0 | 4 |  | pit-service | 2 |
| dpRRTireColdPress | irFloat | 1 | 0 | 4 | Pa | pit-service | 2 |
| dpWindshieldTearoff | irFloat | 1 | 0 | 4 |  | pit-service, weather | 2 |

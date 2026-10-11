# Changelog

## [0.1.0] - 2026-10-11

### Added
- `AudioSystem` static API with lazy / automatic initialization and Domain-Reload-safe static state.
- `SoundDefinition` assets: clip variations, volume/pitch variation, cooldown, instance limits, ignore pause, music ducking, 3D settings.
- Pooled voices with priority-based stealing; `SoundHandle` with stale-handle protection.
- Music crossfade, pause/resume with interruptible fades, ducking.
- Mixer-based volume channels with `IVolumeStorage` (PlayerPrefs default).
- Inspectors with validation and clip preview; `Tools > MLin Audio > Create Settings Asset`.
- EditMode and PlayMode tests, Demo sample.

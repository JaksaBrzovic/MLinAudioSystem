# Changelog

## [Unreleased]

### Added
- Equal-power music crossfades: `AudioSystemSettings.MusicFadeCurve` (default `EqualPower`, `Linear` keeps the old behavior), `AudioMath.FadeGain`.
- `SoundDefinition.ResumeMusicPosition`: music continues where it last stopped when played again.
- `PlayMusic` on a track that is still fading out fades that same voice back in instead of starting a second copy.
- Bus ducking rules (`AudioSystemSettings.DuckingRules`): a mixer group lowers other groups by a set dB with its own attack and release.
- Mixer snapshots with prioritized requests: `AudioSystem.RequestSnapshot` / `ReleaseSnapshot`, `SnapshotHandle`, default snapshot in the settings.
- `SoundDefinition.IntroClip`: plays once, then the main clip follows sample-accurately (also across a pause).
- Beat / bar synced music switches: `SoundDefinition.Bpm` + `BeatsPerBar`, `PlayMusic(..., sync: MusicSync.NextBeat / NextBar)`.
- Mute when the application loses focus (`AudioSystemSettings.MuteWhenUnfocused`, `AudioSystem.MuteWhenUnfocused`).
- Inspector validation for ducking rules, snapshots, the mixer update mode and streaming intro clips.
- Read-only diagnostics on `AudioSystemHost`: `MusicDuckGain`, `FocusGain`, `GetGroupDuckGain(group)`, `GetPlayingClip(handle)`, `GetPlaybackTime(handle)`.

### Changed
- `PlayMusic` has an optional `sync` parameter; existing calls are unchanged.
- **Breaking:** constants follow UPPER_SNAKE_CASE: `AudioMath.MinDecibels` → `AudioMath.MIN_DECIBELS`,
  `AudioSystemSettings.DefaultResourcesPath` → `AudioSystemSettings.DEFAULT_RESOURCES_PATH`.
- **Breaking:** `VolumeChannel` fields are private; read them through `Name`, `ExposedParameter` and `DefaultVolume`.
  Serialized data is unchanged, existing settings assets keep their channels.
- Demo sample UI rebuilt as a uGUI prefab (`DemoUi`, `VolumeSliderRow`) with TextMeshPro instead of `OnGUI`;
  it now requires `com.unity.ugui` and `com.unity.textmeshpro`. The demo releases its runtime clips and definitions on destroy.
- Code style aligned with the project conventions (braces, member order, serialized field layout). No behavior changes.

## [0.1.0] - 2026-10-11

### Added
- `AudioSystem` static API with lazy / automatic initialization and Domain-Reload-safe static state.
- `SoundDefinition` assets: clip variations, volume/pitch variation, cooldown, instance limits, ignore pause, music ducking, 3D settings.
- Pooled voices with priority-based stealing; `SoundHandle` with stale-handle protection.
- Music crossfade, pause/resume with interruptible fades, ducking.
- Mixer-based volume channels with `IVolumeStorage` (PlayerPrefs default).
- Inspectors with validation and clip preview; `Tools > MLin Audio > Create Settings Asset`.
- EditMode and PlayMode tests, Demo sample.

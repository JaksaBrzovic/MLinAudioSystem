# MLin Audio System

Reusable audio system for Unity 2022.3+.

- **Data-driven sounds**: every sound is a `SoundDefinition` asset (clips, volume/pitch variation, mixer group, cooldown, instance limit, 3D settings). No enums or code changes when adding sounds.
- **One static API**: `AudioSystem.Play(sound)`. The system creates itself on first use.
- **Pooled voices** with priority-based voice stealing (music is never stolen).
- **Handles**: `SoundHandle` to stop / re-volume a specific sound. Stale handles are harmless.
- **Music** with crossfade; requesting the track that is already playing does nothing.
- **Pause / Resume** with fades that can be interrupted at any time without volume jumps. UI sounds can ignore pause.
- **Ducking**: a sound can lower the music while it plays.
- **Volume channels** on exposed AudioMixer parameters (linear 0..1 in, dB out), saved through a pluggable `IVolumeStorage` (PlayerPrefs by default, swap in console save data etc.).
- Works with **Domain Reload disabled**, unscaled time everywhere (fades work with `Time.timeScale = 0`).

## Installation

Package Manager → **+** → **Add package from git URL**:

```
https://github.com/JaksaBrzovic/MLinAudioSystem.git?path=/Packages/com.manalink.audiosystem#v0.1.0
```

or in `Packages/manifest.json`:

```json
"com.manalink.audiosystem": "https://github.com/JaksaBrzovic/MLinAudioSystem.git?path=/Packages/com.manalink.audiosystem#v0.1.0"
```

The repository is private, so the machine needs git access to it (Git Credential Manager / `gh auth login`).

## Setup

1. **Settings**: `Tools > MLin Audio > Create Settings Asset` creates `Assets/Resources/AudioSystemSettings.asset`.
   Being in `Resources` with that exact name means the system is created automatically before the first scene loads.
2. **Mixer** (needed for volume sliders): create an Audio Mixer with groups, e.g. `Master`, `Music`, `SFX`, `UI`.
   For each group: select it, right click **Volume** in the inspector → **Expose 'Volume' to script**, then rename the
   parameter in the Audio Mixer window (*Exposed Parameters*, top right) to `MasterVolume`, `MusicVolume`, ...
3. Assign the mixer, the default **Music Group** and **SFX Group**, and list the **Volume Channels** (name + exposed parameter)
   in the settings asset. The inspector shows an error for every parameter it can not find.
4. Create sounds: `Create > MLin > Audio > Sound Definition`. The inspector has a **Preview** button.

The **Demo** sample (Package Manager → MLin Audio System → Samples) contains a ready mixer, settings and a test scene.

## Usage

```csharp
using MLin.Audio;

public class Door : MonoBehaviour
{
    [SerializeField] private SoundDefinition openSound;
    [SerializeField] private SoundDefinition creakLoop;

    private SoundHandle creak;

    public void Open()
    {
        AudioSystem.PlayAt(openSound, transform.position);
        creak = AudioSystem.PlayAttached(creakLoop, transform, fadeIn: 0.2f);
    }

    public void Close()
    {
        creak.Stop(fadeOut: 0.3f);
    }
}
```

| What | API |
|---|---|
| 2D sound | `AudioSystem.Play(sound, fadeIn = 0)` |
| Sound at a position | `AudioSystem.PlayAt(sound, position)` |
| Sound following a transform (auto-stops when it is destroyed) | `AudioSystem.PlayAttached(sound, transform)` |
| Stop one / all of a kind / everything | `handle.Stop(fade)`, `AudioSystem.StopAll(sound, fade)`, `AudioSystem.StopAllSounds(fade, includeMusic)` |
| Music | `AudioSystem.PlayMusic(track, crossfade = default, restartIfPlaying = false)`, `AudioSystem.StopMusic(fade)`, `AudioSystem.CurrentMusic` |
| Pause menu | `AudioSystem.Pause(fade)`, `AudioSystem.Resume(fade)`, `AudioSystem.IsPaused` |
| Settings sliders | `AudioSystem.GetChannelVolume("Music")`, `AudioSystem.SetChannelVolume("Music", 0.5f)` |
| Custom save system | `AudioSystem.SetVolumeStorage(new MyStorage())` |

Fade arguments are seconds of unscaled time. For music and pause, a negative fade (the default) uses the value from settings.

### SoundDefinition fields

| Field | Meaning |
|---|---|
| Clips + Selection Mode | One or more clips; `Random`, `RandomNoRepeat` or `Sequential`. Empty slots are skipped. |
| Output Group | Mixer group. Empty = Music/SFX group from settings. |
| Volume / Pitch (+ Variation) | Base values with random +/- per play. |
| Loop | Looping sounds play until stopped. Music tracks normally loop. |
| Priority | 0 = most important. Decides which sound is stolen when all voices are busy. |
| Cooldown | Minimum seconds between plays (e.g. UI navigation spam). |
| Max Instances + Limit Mode | Simultaneous instances; `StopOldest` fades the oldest out, `RejectNew` ignores the request. |
| Ignore Pause | Keeps playing while paused (pause menu UI sounds). |
| Duck Music | 0..1 how much music is lowered while this sound plays. |
| 3D | Spatial blend, distances, rolloff, doppler. Used by `PlayAt` / `PlayAttached` only; `Play` is always 2D. |

### Pause

`AudioSystem.Pause` fades every sound (except *Ignore Pause* ones) to silence, then sets `AudioListener.pause`.
`Resume` can be called at any moment, including in the middle of the pause fade, and continues from the current level.
Sounds that finished or were stopped before the pause are never restarted. If your project also sets
`AudioListener.pause` directly, let the audio system own it instead.

### Custom volume storage

```csharp
public class SaveDataVolumeStorage : IVolumeStorage
{
    public bool TryLoad(string channel, out float volume) => MySaveData.Volumes.TryGetValue(channel, out volume);
    public void Store(string channel, float volume) => MySaveData.Volumes[channel] = volume;
    public void Flush() => MySaveData.Save();
}

// early, e.g. in your bootstrap:
AudioSystem.SetVolumeStorage(new SaveDataVolumeStorage());
```

### Event-driven projects

The package intentionally has no global event bus. If a project already plays sounds through events, add a small adapter
in the project:

```csharp
public class AudioEventAdapter : MonoBehaviour
{
    [SerializeField] private SoundDefinition cardFlip;

    private void OnEnable() => GameEvents.OnCardFlipped += HandleCardFlipped;
    private void OnDisable() => GameEvents.OnCardFlipped -= HandleCardFlipped; // named method, so unsubscribe works

    private void HandleCardFlipped() => AudioSystem.Play(cardFlip);
}
```

## Lifetime

- With `Resources/AudioSystemSettings` (Auto Initialize on): created before the first scene loads.
- Otherwise: created on the first API call, using built-in defaults, or explicitly via `AudioSystem.Initialize(settings)`.
- An `AudioSystemHost` component can also be placed in a scene with a settings asset; it runs early (execution order -1000).
- Only one host ever exists; duplicates destroy themselves with a warning.
- `AudioSystem.Shutdown()` stops everything; the next call creates the system again.

## Notes

- `AudioMixer.SetFloat` is ignored during `Awake`, so saved volumes are applied in `Start` of the host.
- Volumes are never written to `AudioSource.volume`; fades, pause and ducking are, so they can not overwrite each other.
- Playback end is tracked with an internal timer frozen during pause, so it works while paused and when audio output is disabled (batch mode, tests).

# Audio Playground

Test bench for the MLin Audio System in this host project (not part of the package). Open
`AudioPlayground.unity`, press Play and listen.

## What to try

| Feature | How |
|---|---|
| Crossfade (equal power) | Play *World*, then *Town* with *Now*. Change the crossfade slider (0-5 s). |
| Intro + loop | *World* starts with a 4-bar intro, then its 28-bar loop follows without a gap ("Position" shows Intro/Loop). |
| Resume position | World → Town → World: World continues where it stopped. Tick *Restart from the beginning* to compare. |
| Back during a fade | Switch World → Town and immediately back: the same World playback fades back in. |
| Beat / bar sync | *Next beat* / *Next bar*: the switch waits for the current track's grid (watch "Position"). |
| Bus ducking | Turn on *Wind loop* and *Conversation* while music plays: music drops 10 dB and wind 8 dB while a line is spoken. |
| Duck Music | *Explosion* lowers the music by 70 % for its duration. |
| Limits and variations | *Click* (5 variations, no repeats), *Footstep* (max 2), *Sword hit x10* (max 3, oldest stopped). |
| Snapshots | *Underwater* (everything muffled), *Pause menu* (music muffled, priority 10). With both on the menu wins; turning it off falls back to underwater. |
| Pause | *Pause* fades everything except *Confirm* (Ignore Pause) - press Confirm while paused. |
| Focus | Alt-tab away: audio mutes, coming back fades it in (enabled for the Editor in `PlaygroundAudioSettings`). |
| Volume channels | Sliders write the exposed mixer parameters and are saved (PlayerPrefs prefix `MLin.Audio.Playground.Volume.`). |

Everything is tunable live: select `PlaygroundAudioSettings` (ducking rules, fades, focus) or a definition in
`Definitions/` (variations, limits, Duck Music, Bpm) while playing.

## Music preparation

| Track | Source | Tempo | Prepared as |
|---|---|---|---|
| World | *Shakkar* | 140 BPM 4/4 (measured; the file is exactly 32 bars) | `WorldIntro` = bars 1-4 (302 400 samples), `WorldLoop` = bars 5-32 (2 116 800 samples) |
| Town | *Medieval fair loop* | 130 BPM 4/4 (measured) | `TownLoop` = exactly 8 bars (651 323 samples); the reverb tail after bar 8 is mixed into the start so the loop has no gap |

Tempo was measured with an onset autocorrelation and cross-checked against the clip lengths. Music and ambience
import as *Compressed In Memory* (reliable sample-accurate joins), short sounds as *Decompress On Load* (ADPCM).

Sources and licenses: [CREDITS.md](CREDITS.md).

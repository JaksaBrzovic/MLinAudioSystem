AI-Native project developed by Jaksa Brzovic

<!-- usage:start -->
Claude Code time: 0 h 42 min of work over 1 session

| Claude Code usage | Input | Output | Cache write 5m | Cache write 1h | Cache read | Web searches | Web fetches |
|---|---:|---:|---:|---:|---:|---:|---:|
| claude-opus-5-5 | 246 | 217,441 | 0 | 1,068,489 | 39,687,112 | 0 | 0 |
<!-- usage-data: {"models": {"claude-opus-5-5": {"cache_read": 39687112, "cache_write_1h": 1068489, "cache_write_5m": 0, "input": 246, "output": 217441, "web_fetches": 0, "web_searches": 0}}, "seconds": 2557.179, "sessions": 1, "until": "2026-10-14T18:09:50+00:00"} -->
<!-- usage:end -->

# MLinAudioSystem

Development project for the **MLin Audio System** Unity package (`com.manalink.audiosystem`).

- Package source: [`Packages/com.manalink.audiosystem`](Packages/com.manalink.audiosystem) (documentation in its [README](Packages/com.manalink.audiosystem/README.md))
- Host project: Unity 2022.3.62f3, used to develop and test the package.

## Development

- Open this folder in Unity 2022.3. The package is embedded, so edits go straight to the package.
- Tests: Window > General > Test Runner (EditMode + PlayMode), or from the command line:
  `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results.xml`
- Audio Playground: `Assets/AudioPlayground/AudioPlayground.unity` is a test bench with real (CC0) music and sounds
  for crossfades, intro/loop, beat sync, ducking, snapshots, pause and focus. See its [README](Assets/AudioPlayground/README.md).
- Demo: import it from Package Manager > MLin Audio System > Samples.
  `Samples~` is hidden from Unity, so to change the demo copy it into `Assets`, edit it, and copy it back with its `.meta` files.

## Releasing

1. Bump `version` in `package.json` and add a `CHANGELOG.md` entry.
2. Commit, then tag: `git tag v0.1.0 && git push --tags`.
3. Projects reference `...MLinAudioSystem.git?path=/Packages/com.manalink.audiosystem#v0.1.0`.

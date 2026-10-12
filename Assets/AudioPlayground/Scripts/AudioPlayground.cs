using UnityEngine;
using UnityEngine.Audio;

namespace MLin.Audio.Playground
{
    /// <summary>
    /// Test bench for the audio system: turns panel requests into AudioSystem calls and shows what the system is doing
    /// (music position on the beat grid, ducking per bus, active snapshot, pause and focus).
    /// </summary>
    public class AudioPlayground : MonoBehaviour
    {
        private const float DEFAULT_CROSSFADE = 2f;
        private const float WIND_FADE = 1.5f;
        private const float CONVERSATION_GAP = 0.7f;
        private const int SWORD_BURST_COUNT = 10;
        private const float PAUSE_FADE = 0.5f;
        private const float SNAPSHOT_TRANSITION = 0.5f;
        private const int UNDERWATER_PRIORITY = 0;
        private const int MENU_PRIORITY = 10;

        [SerializeField] private PlaygroundPanelView panelView;

        [Header("Music")]
        [SerializeField] private SoundDefinition worldMusic;
        [SerializeField] private SoundDefinition townMusic;

        [Header("Sounds")]
        [SerializeField] private SoundDefinition click;
        [SerializeField] private SoundDefinition confirm;
        [SerializeField] private SoundDefinition error;
        [SerializeField] private SoundDefinition footstep;
        [SerializeField] private SoundDefinition swordHit;
        [SerializeField] private SoundDefinition explosion;
        [SerializeField] private SoundDefinition dialogueLine;
        [SerializeField] private SoundDefinition wind;

        [Header("Mix")]
        [SerializeField] private AudioMixerSnapshot underwaterSnapshot;
        [SerializeField] private AudioMixerSnapshot menuSnapshot;
        [Tooltip("Only used to show how much the bus ducking rules lower these groups.")]
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;

        private float crossfade = DEFAULT_CROSSFADE;
        private bool isRestartingMusic;
        private bool isConversationOn;
        private SoundHandle lineHandle;
        private float nextLineTime;
        private SoundHandle windHandle;
        private SnapshotHandle underwaterRequest;
        private SnapshotHandle menuRequest;

        // last shown status; a line is rebuilt only when its inputs change
        private bool hasShownMusic;
        private SoundDefinition shownMusic;
        private int shownPositionKey = int.MinValue;
        private int shownActiveVoices = -1;
        private int shownPooledVoices = -1;
        private int shownDuckingKey = int.MinValue;
        private int shownMixKey = -1;

        #region UNITY LIFECYCLE
        private void Awake()
        {
            if (!HasAllReferences())
            {
                Debug.LogError($"[Audio Playground] '{name}' has unassigned references. Select it in the scene and assign every field.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (panelView == null)
            {
                return;
            }

            panelView.ActionRequested += HandleActionRequested;
            panelView.SwitchChanged += HandleSwitchChanged;
            panelView.CrossfadeChanged += HandleCrossfadeChanged;
            panelView.VolumeChanged += HandleVolumeChanged;
        }

        private void Start()
        {
            panelView.ShowCrossfade(crossfade);
            panelView.SetSwitch(PlaygroundSwitch.MuteWhenUnfocused, AudioSystem.MuteWhenUnfocused);

            if (AudioSystem.Settings == null)
            {
                return;
            }

            foreach (VolumeChannel channel in AudioSystem.Settings.VolumeChannels)
            {
                if (channel != null && !string.IsNullOrEmpty(channel.Name))
                {
                    panelView.AddVolumeRow(channel.Name, AudioSystem.GetChannelVolume(channel.Name));
                }
            }
        }

        private void Update()
        {
            UpdateConversation();
            RefreshStatus();
        }

        private void OnDisable()
        {
            if (panelView == null)
            {
                return;
            }

            panelView.ActionRequested -= HandleActionRequested;
            panelView.SwitchChanged -= HandleSwitchChanged;
            panelView.CrossfadeChanged -= HandleCrossfadeChanged;
            panelView.VolumeChanged -= HandleVolumeChanged;
        }

        private void OnDestroy()
        {
            // snapshot requests belong to this bench; give the mix back when it goes away
            AudioSystem.ReleaseSnapshot(underwaterRequest, 0f);
            AudioSystem.ReleaseSnapshot(menuRequest, 0f);
        }
        #endregion

        #region HANDLERS
        private void HandleActionRequested(PlaygroundAction action)
        {
            switch (action)
            {
                case PlaygroundAction.WorldNow:
                    PlayMusic(worldMusic, MusicSync.Immediate);
                    break;

                case PlaygroundAction.TownNow:
                    PlayMusic(townMusic, MusicSync.Immediate);
                    break;

                case PlaygroundAction.WorldNextBeat:
                    PlayMusic(worldMusic, MusicSync.NextBeat);
                    break;

                case PlaygroundAction.TownNextBeat:
                    PlayMusic(townMusic, MusicSync.NextBeat);
                    break;

                case PlaygroundAction.WorldNextBar:
                    PlayMusic(worldMusic, MusicSync.NextBar);
                    break;

                case PlaygroundAction.TownNextBar:
                    PlayMusic(townMusic, MusicSync.NextBar);
                    break;

                case PlaygroundAction.StopMusic:
                    AudioSystem.StopMusic(crossfade);
                    break;

                case PlaygroundAction.Click:
                    AudioSystem.Play(click);
                    break;

                case PlaygroundAction.Confirm:
                    AudioSystem.Play(confirm);
                    break;

                case PlaygroundAction.Error:
                    AudioSystem.Play(error);
                    break;

                case PlaygroundAction.Footstep:
                    AudioSystem.Play(footstep);
                    break;

                case PlaygroundAction.SwordHit:
                    AudioSystem.Play(swordHit);
                    break;

                case PlaygroundAction.SwordHitBurst:
                    for (int i = 0; i < SWORD_BURST_COUNT; i++)
                    {
                        AudioSystem.Play(swordHit);
                    }
                    break;

                case PlaygroundAction.Explosion:
                    AudioSystem.Play(explosion);
                    break;

                case PlaygroundAction.SayLine:
                    lineHandle = AudioSystem.Play(dialogueLine);
                    break;

                case PlaygroundAction.Pause:
                    AudioSystem.Pause(PAUSE_FADE);
                    break;

                case PlaygroundAction.Resume:
                    AudioSystem.Resume(PAUSE_FADE);
                    break;
            }
        }

        private void HandleSwitchChanged(PlaygroundSwitch target, bool isOn)
        {
            switch (target)
            {
                case PlaygroundSwitch.RestartMusic:
                    isRestartingMusic = isOn;
                    break;

                case PlaygroundSwitch.Conversation:
                    isConversationOn = isOn;
                    nextLineTime = Time.unscaledTime;
                    break;

                case PlaygroundSwitch.Wind:
                    if (isOn && !windHandle.IsPlaying)
                    {
                        windHandle = AudioSystem.Play(wind, WIND_FADE);
                    }
                    else if (!isOn)
                    {
                        windHandle.Stop(WIND_FADE);
                    }
                    break;

                case PlaygroundSwitch.UnderwaterSnapshot:
                    underwaterRequest = SetSnapshotRequest(underwaterRequest, underwaterSnapshot, UNDERWATER_PRIORITY, isOn);
                    break;

                case PlaygroundSwitch.MenuSnapshot:
                    menuRequest = SetSnapshotRequest(menuRequest, menuSnapshot, MENU_PRIORITY, isOn);
                    break;

                case PlaygroundSwitch.MuteWhenUnfocused:
                    AudioSystem.MuteWhenUnfocused = isOn;
                    break;
            }
        }

        private void HandleCrossfadeChanged(float seconds)
        {
            crossfade = seconds;
        }

        private void HandleVolumeChanged(string channel, float volume)
        {
            AudioSystem.SetChannelVolume(channel, volume);
        }
        #endregion

        private void PlayMusic(SoundDefinition music, MusicSync sync)
        {
            AudioSystem.PlayMusic(music, crossfade, isRestartingMusic, sync);
        }

        private static SnapshotHandle SetSnapshotRequest(SnapshotHandle current, AudioMixerSnapshot snapshot, int priority, bool isOn)
        {
            if (isOn)
            {
                return current.IsValid ? current : AudioSystem.RequestSnapshot(snapshot, priority, SNAPSHOT_TRANSITION);
            }

            current.Release(SNAPSHOT_TRANSITION);
            return SnapshotHandle.Invalid;
        }

        private void UpdateConversation()
        {
            if (!isConversationOn)
            {
                return;
            }

            // the next line waits for the previous one plus a short gap, like people taking turns
            if (lineHandle.IsPlaying)
            {
                nextLineTime = Time.unscaledTime + CONVERSATION_GAP;
                return;
            }

            if (Time.unscaledTime >= nextLineTime)
            {
                lineHandle = AudioSystem.Play(dialogueLine);
            }
        }

        #region STATUS
        private void RefreshStatus()
        {
            AudioSystemHost host = AudioSystem.Host;
            if (host == null)
            {
                return;
            }

            RefreshMusic(host);
            RefreshVoices(host);
            RefreshDucking(host);
            RefreshMix(host);
        }

        private void RefreshMusic(AudioSystemHost host)
        {
            SoundDefinition music = AudioSystem.CurrentMusic;
            if (!hasShownMusic || music != shownMusic)
            {
                hasShownMusic = true;
                shownMusic = music;
                panelView.ShowMusic(music != null ? $"Music: {music.name}  ({music.Bpm:0} BPM, {music.BeatsPerBar}/4)" : "Music: -");
            }

            bool isIntro = false;
            int barIndex = 0;
            int beatIndex = 0;
            int positionKey = -1;
            if (music != null && music.Bpm > 0f)
            {
                SoundHandle handle = AudioSystem.CurrentMusicHandle;
                AudioClip clip = host.GetPlayingClip(handle);
                float time = host.GetPlaybackTime(handle);
                if (clip != null && time >= 0f)
                {
                    float beat = 60f / music.Bpm;
                    float bar = beat * music.BeatsPerBar;
                    isIntro = clip == music.IntroClip;
                    barIndex = (int)(time / bar);
                    beatIndex = (int)((time - barIndex * bar) / beat);
                    positionKey = (isIntro ? 1000000 : 0) + barIndex * 100 + beatIndex;
                }
            }

            if (positionKey == shownPositionKey)
            {
                return;
            }

            shownPositionKey = positionKey;
            panelView.ShowPosition(positionKey < 0 ? "Position: -" : $"Position: {(isIntro ? "Intro" : "Loop")}  bar {barIndex + 1}  beat {beatIndex + 1}");
        }

        private void RefreshVoices(AudioSystemHost host)
        {
            int activeVoices = host.ActiveVoiceCount;
            int pooledVoices = host.VoiceCount;
            if (activeVoices == shownActiveVoices && pooledVoices == shownPooledVoices)
            {
                return;
            }

            shownActiveVoices = activeVoices;
            shownPooledVoices = pooledVoices;
            panelView.ShowVoices(activeVoices, pooledVoices);
        }

        private void RefreshDucking(AudioSystemHost host)
        {
            int musicRules = Mathf.RoundToInt(AudioMath.LinearToDecibels(host.GetGroupDuckGain(musicGroup)));
            int duckMusic = Mathf.RoundToInt(AudioMath.LinearToDecibels(host.MusicDuckGain));
            int ambience = Mathf.RoundToInt(AudioMath.LinearToDecibels(host.GetGroupDuckGain(ambienceGroup)));
            int duckingKey = (musicRules + 100) * 1000000 + (duckMusic + 100) * 1000 + (ambience + 100);
            if (duckingKey == shownDuckingKey)
            {
                return;
            }

            shownDuckingKey = duckingKey;
            panelView.ShowDucking($"Ducking  Music: {musicRules} dB rules, {duckMusic} dB Duck Music   Ambience: {ambience} dB");
        }

        private void RefreshMix(AudioSystemHost host)
        {
            int snapshot = menuRequest.IsValid ? 2 : underwaterRequest.IsValid ? 1 : 0;
            bool isPaused = AudioSystem.IsPaused;
            bool isMuted = host.FocusGain < 1f;
            int mixKey = snapshot * 4 + (isPaused ? 2 : 0) + (isMuted ? 1 : 0);
            if (mixKey == shownMixKey)
            {
                return;
            }

            shownMixKey = mixKey;
            string snapshotName = snapshot == 2 ? "Pause menu (priority 10)" : snapshot == 1 ? "Underwater" : "Default";
            panelView.ShowMix($"Snapshot: {snapshotName}   Paused: {(isPaused ? "yes" : "no")}   Focus: {(isMuted ? "muted" : "ok")}");
        }
        #endregion

        private bool HasAllReferences()
        {
            return panelView != null && worldMusic != null && townMusic != null
                && click != null && confirm != null && error != null && footstep != null && swordHit != null
                && explosion != null && dialogueLine != null && wind != null
                && underwaterSnapshot != null && menuSnapshot != null && musicGroup != null && ambienceGroup != null;
        }
    }
}

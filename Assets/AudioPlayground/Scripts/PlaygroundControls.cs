using System;
using UnityEngine;
using UnityEngine.UI;

namespace MLin.Audio.Playground
{
    /// <summary>One-shot requests from the playground panel.</summary>
    public enum PlaygroundAction
    {
        WorldNow,
        TownNow,
        WorldNextBeat,
        TownNextBeat,
        WorldNextBar,
        TownNextBar,
        StopMusic,
        Click,
        Confirm,
        Error,
        Footstep,
        SwordHit,
        SwordHitBurst,
        Explosion,
        SayLine,
        Pause,
        Resume
    }

    /// <summary>On/off switches on the playground panel.</summary>
    public enum PlaygroundSwitch
    {
        RestartMusic,
        Conversation,
        Wind,
        UnderwaterSnapshot,
        MenuSnapshot,
        MuteWhenUnfocused
    }

    /// <summary>Binds a prefab button to the action it requests.</summary>
    [Serializable]
    public struct ActionButton
    {
        [SerializeField] private PlaygroundAction action;
        [SerializeField] private Button button;

        public PlaygroundAction Action => action;
        public Button Button => button;
    }

    /// <summary>Binds a prefab toggle to the switch it controls.</summary>
    [Serializable]
    public struct SwitchToggle
    {
        [SerializeField] private PlaygroundSwitch target;
        [SerializeField] private Toggle toggle;

        public PlaygroundSwitch Target => target;
        public Toggle Toggle => toggle;
    }
}

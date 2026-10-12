using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace MLin.Audio.Editor
{
    [CustomEditor(typeof(AudioSystemSettings))]
    internal sealed class AudioSystemSettingsEditor : UnityEditor.Editor
    {
        private const string DEFAULT_ASSET_FOLDER = "Assets/Resources";

        public override void OnInspectorGUI()
        {
            AudioSystemSettings settings = (AudioSystemSettings)target;
            DrawLocationInfo(settings);
            DrawDefaultInspector();
            DrawValidation(settings);
        }

        private static void DrawLocationInfo(AudioSystemSettings settings)
        {
            string path = AssetDatabase.GetAssetPath(settings);
            bool isAutoLoaded = path.Contains("/Resources/") && Path.GetFileNameWithoutExtension(path) == AudioSystemSettings.DEFAULT_RESOURCES_PATH;

            if (isAutoLoaded)
            {
                EditorGUILayout.HelpBox("Loaded automatically from Resources.", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox($"Not loaded automatically. Move it to a Resources folder as '{AudioSystemSettings.DEFAULT_RESOURCES_PATH}', assign it to an AudioSystemHost or pass it to AudioSystem.Initialize.", MessageType.Info);
            }
        }

        private static void DrawValidation(AudioSystemSettings settings)
        {
            if (settings.Mixer == null)
            {
                EditorGUILayout.HelpBox("No AudioMixer assigned: volume channels will have no audible effect.", MessageType.Warning);
                return;
            }

            HashSet<string> names = new HashSet<string>();
            foreach (VolumeChannel channel in settings.VolumeChannels)
            {
                if (channel == null || string.IsNullOrEmpty(channel.Name))
                {
                    EditorGUILayout.HelpBox("A volume channel has no name.", MessageType.Warning);
                    continue;
                }

                if (!names.Add(channel.Name))
                {
                    EditorGUILayout.HelpBox($"Duplicate volume channel '{channel.Name}'.", MessageType.Warning);
                }

                if (string.IsNullOrEmpty(channel.ExposedParameter) || !settings.Mixer.GetFloat(channel.ExposedParameter, out _))
                {
                    EditorGUILayout.HelpBox($"Channel '{channel.Name}': exposed parameter '{channel.ExposedParameter}' not found on '{settings.Mixer.name}'. In the Audio Mixer window right click a group's Volume > Expose, then rename it under Exposed Parameters.", MessageType.Error);
                }
            }

            if (settings.MusicGroup != null && settings.MusicGroup.audioMixer != settings.Mixer)
            {
                EditorGUILayout.HelpBox("Music Group belongs to a different mixer.", MessageType.Warning);
            }

            if (settings.SfxGroup != null && settings.SfxGroup.audioMixer != settings.Mixer)
            {
                EditorGUILayout.HelpBox("SFX Group belongs to a different mixer.", MessageType.Warning);
            }

            DrawDuckingValidation(settings);
            DrawSnapshotValidation(settings);
        }

        private static void DrawDuckingValidation(AudioSystemSettings settings)
        {
            for (int i = 0; i < settings.DuckingRules.Length; i++)
            {
                DuckingRule rule = settings.DuckingRules[i];
                if (rule == null || rule.Trigger == null || rule.Target == null)
                {
                    EditorGUILayout.HelpBox($"Ducking rule {i}: assign both Trigger and Target groups, otherwise the rule does nothing.", MessageType.Warning);
                    continue;
                }

                if (rule.Trigger == rule.Target)
                {
                    EditorGUILayout.HelpBox($"Ducking rule {i}: Trigger and Target are the same group, the rule is ignored.", MessageType.Warning);
                }

                if (rule.Trigger.audioMixer != settings.Mixer || rule.Target.audioMixer != settings.Mixer)
                {
                    EditorGUILayout.HelpBox($"Ducking rule {i} uses a group from a different mixer.", MessageType.Warning);
                }
            }
        }

        private static void DrawSnapshotValidation(AudioSystemSettings settings)
        {
            if (settings.DefaultSnapshot != null && settings.DefaultSnapshot.audioMixer != settings.Mixer)
            {
                EditorGUILayout.HelpBox("Default Snapshot belongs to a different mixer.", MessageType.Warning);
            }

            if (settings.DefaultSnapshot == null && settings.Mixer.FindSnapshot("Snapshot") == null)
            {
                EditorGUILayout.HelpBox("No Default Snapshot and the mixer has no snapshot named 'Snapshot': released snapshots have nothing to return to.", MessageType.Warning);
            }

            if (settings.Mixer.updateMode != AudioMixerUpdateMode.UnscaledTime)
            {
                EditorGUILayout.HelpBox("The mixer's Update Mode is Normal: snapshot transitions and mixer effects stop while Time.timeScale is 0 (e.g. a pause menu). Set Update Mode to Unscaled Time on the mixer asset.", MessageType.Info);
            }
        }

        [MenuItem("Tools/MLin Audio/Create Settings Asset")]
        private static void CreateSettingsAsset()
        {
            string path = $"{DEFAULT_ASSET_FOLDER}/{AudioSystemSettings.DEFAULT_RESOURCES_PATH}.asset";
            AudioSystemSettings existing = AssetDatabase.LoadAssetAtPath<AudioSystemSettings>(path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!AssetDatabase.IsValidFolder(DEFAULT_ASSET_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            AudioSystemSettings settings = CreateInstance<AudioSystemSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MLin.Audio.Editor
{
    [CustomEditor(typeof(AudioSystemSettings))]
    internal sealed class AudioSystemSettingsEditor : UnityEditor.Editor
    {
        private const string DefaultAssetFolder = "Assets/Resources";

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
            bool isAutoLoaded = path.Contains("/Resources/") && Path.GetFileNameWithoutExtension(path) == AudioSystemSettings.DefaultResourcesPath;

            if (isAutoLoaded)
                EditorGUILayout.HelpBox("Loaded automatically from Resources.", MessageType.None);
            else
                EditorGUILayout.HelpBox($"Not loaded automatically. Move it to a Resources folder as '{AudioSystemSettings.DefaultResourcesPath}', assign it to an AudioSystemHost or pass it to AudioSystem.Initialize.", MessageType.Info);
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
                if (channel == null || string.IsNullOrEmpty(channel.name))
                {
                    EditorGUILayout.HelpBox("A volume channel has no name.", MessageType.Warning);
                    continue;
                }

                if (!names.Add(channel.name))
                    EditorGUILayout.HelpBox($"Duplicate volume channel '{channel.name}'.", MessageType.Warning);

                if (string.IsNullOrEmpty(channel.exposedParameter) || !settings.Mixer.GetFloat(channel.exposedParameter, out _))
                    EditorGUILayout.HelpBox($"Channel '{channel.name}': exposed parameter '{channel.exposedParameter}' not found on '{settings.Mixer.name}'. In the Audio Mixer window right click a group's Volume > Expose, then rename it under Exposed Parameters.", MessageType.Error);
            }

            if (settings.MusicGroup != null && settings.MusicGroup.audioMixer != settings.Mixer)
                EditorGUILayout.HelpBox("Music Group belongs to a different mixer.", MessageType.Warning);

            if (settings.SfxGroup != null && settings.SfxGroup.audioMixer != settings.Mixer)
                EditorGUILayout.HelpBox("SFX Group belongs to a different mixer.", MessageType.Warning);
        }

        [MenuItem("Tools/MLin Audio/Create Settings Asset")]
        private static void CreateSettingsAsset()
        {
            string path = $"{DefaultAssetFolder}/{AudioSystemSettings.DefaultResourcesPath}.asset";
            AudioSystemSettings existing = AssetDatabase.LoadAssetAtPath<AudioSystemSettings>(path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!AssetDatabase.IsValidFolder(DefaultAssetFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            AudioSystemSettings settings = CreateInstance<AudioSystemSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}

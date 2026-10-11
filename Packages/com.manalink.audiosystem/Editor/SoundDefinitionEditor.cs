using UnityEditor;
using UnityEngine;

namespace MLin.Audio.Editor
{
    [CustomEditor(typeof(SoundDefinition))]
    [CanEditMultipleObjects]
    internal sealed class SoundDefinitionEditor : UnityEditor.Editor
    {
        private AudioSource previewSource;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length > 1)
                return;

            SoundDefinition definition = (SoundDefinition)target;
            DrawValidation(definition);
            DrawPreview(definition);
        }

        private static void DrawValidation(SoundDefinition definition)
        {
            if (!definition.HasClips)
            {
                EditorGUILayout.HelpBox("No clips assigned, this sound will not play.", MessageType.Warning);
                return;
            }

            foreach (AudioClip clip in definition.Clips)
            {
                if (clip == null)
                {
                    EditorGUILayout.HelpBox("Clips list contains empty entries, they are skipped.", MessageType.Info);
                    break;
                }
            }

            if (definition.Loop && definition.MaxInstances == 0 && definition.Cooldown <= 0f)
                EditorGUILayout.HelpBox("Looping sound without Max Instances: every Play starts another loop. Keep the handle and Stop it, or set Max Instances to 1.", MessageType.Info);
        }

        private void DrawPreview(SoundDefinition definition)
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!definition.HasClips))
                {
                    if (GUILayout.Button("▶ Preview"))
                        PlayPreview(definition);
                }

                if (GUILayout.Button("■ Stop"))
                    StopPreview();
            }
        }

        private void PlayPreview(SoundDefinition definition)
        {
            if (previewSource == null)
            {
                GameObject previewObject = EditorUtility.CreateGameObjectWithHideFlags("MLin Audio Preview", HideFlags.HideAndDontSave, typeof(AudioSource));
                previewSource = previewObject.GetComponent<AudioSource>();
                previewSource.playOnAwake = false;
            }

            previewSource.Stop();
            previewSource.clip = definition.PickClip();
            previewSource.volume = definition.PickVolume();
            previewSource.pitch = definition.PickPitch();
            previewSource.loop = definition.Loop;
            previewSource.outputAudioMixerGroup = definition.OutputGroup;
            previewSource.Play();
        }

        private void StopPreview()
        {
            if (previewSource != null)
                previewSource.Stop();
        }

        private void OnDisable()
        {
            if (previewSource != null)
                DestroyImmediate(previewSource.gameObject);
        }
    }
}

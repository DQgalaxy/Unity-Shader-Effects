using UnityEditor;
using UnityEngine;

namespace PortfolioFX.Editor
{
    [CustomEditor(typeof(FXSequenceDriver))]
    public sealed class FXSequenceDriverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var driver = (FXSequenceDriver)target;
            if (driver.gameObject.scene.path == "Assets/PortfolioFX/Scenes/CrystalCollapse.unity")
            {
                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("Setup Crystal Collapse Presentation"))
                        EditorApplication.delayCall += global::PortfolioFX.EditorTools.CrystalCollapseSetup.Apply;
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sequence Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play")) driver.Play();
                if (GUILayout.Button("Pause")) driver.Pause();
                if (GUILayout.Button("Reset")) driver.ResetSequence();
            }
            EditorGUI.BeginChangeCheck();
            float t = EditorGUILayout.Slider("Time (seconds)", driver.CurrentTime, 0, driver.duration);
            if (EditorGUI.EndChangeCheck())
            {
                driver.Pause();
                driver.Seek(t);
                SceneView.RepaintAll();
            }
            EditorGUILayout.HelpBox("Use Play Mode to run continuously. The time slider samples the clip in Edit Mode. Preview materials are placeholders; disable Preview Placeholder after assigning your graph.", MessageType.Info);
        }
    }
}

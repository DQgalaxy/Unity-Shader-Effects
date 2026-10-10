using UnityEditor;
using UnityEngine;

namespace PortfolioFX.Editor
{
    [CustomEditor(typeof(FXGroundSlashLesson))]
    public sealed class FXGroundSlashLessonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var fx = (FXGroundSlashLesson)target;
            DrawDefaultInspector();
            if (fx.ConfigurationError != null)
            {
                EditorGUILayout.HelpBox(fx.ConfigurationError, MessageType.Error);
                return;
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sequence Preview", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float time = EditorGUILayout.Slider("Time (seconds)", fx.sequence.CurrentTime, 0, fx.sequence.duration);
            if (EditorGUI.EndChangeCheck()) Sample(fx, time);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Palm 0.50")) Sample(fx, .50f);
                if (GUILayout.Button("Flight 0.85")) Sample(fx, .85f);
                if (GUILayout.Button("Impact 1.10")) Sample(fx, 1.10f);
                if (GUILayout.Button("Reset")) Sample(fx, 0);
            }
            if (GUILayout.Button("Rebuild Presentation"))
            {
                fx.Rebuild(); fx.Refresh(); SceneView.RepaintAll();
            }
        }

        static void Sample(FXGroundSlashLesson fx, float time)
        {
            fx.sequence.Pause();
            fx.sequence.Seek(time);
            fx.Refresh();
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}

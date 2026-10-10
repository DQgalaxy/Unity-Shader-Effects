using UnityEditor;
using UnityEngine;

namespace PortfolioFX.Editor
{
    public abstract class FXRemainingPresentationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var component=(MonoBehaviour)target;
            var driver=component.GetComponent<FXSequenceDriver>();
            if(!driver)return;
            EditorGUILayout.Space();EditorGUILayout.LabelField("Sequence Preview",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float t=EditorGUILayout.Slider("Time (seconds)",driver.CurrentTime,0,driver.duration);
            if(EditorGUI.EndChangeCheck()){
                driver.Pause();driver.Seek(t);Refresh(component);SceneView.RepaintAll();EditorApplication.QueuePlayerLoopUpdate();
            }
            if(GUILayout.Button("Reset Preview")){
                driver.ResetSequence();Refresh(component);SceneView.RepaintAll();
            }
        }
        public static void Refresh(MonoBehaviour component)
        {
            if(component is FXMeteorPresentation meteor)meteor.Refresh();
            else if(component is FXPortalPresentation portal)portal.Refresh();
            else if(component is FXWaterPresentation water)water.Refresh();
            else if(component is FXDepthLabPresentation depth)depth.Refresh();
        }
    }
    [CustomEditor(typeof(FXMeteorPresentation))]public sealed class FXMeteorPresentationEditor:FXRemainingPresentationEditor{}
    [CustomEditor(typeof(FXPortalPresentation))]public sealed class FXPortalPresentationEditor:FXRemainingPresentationEditor{}
    [CustomEditor(typeof(FXWaterPresentation))]public sealed class FXWaterPresentationEditor:FXRemainingPresentationEditor{}
    [CustomEditor(typeof(FXDepthLabPresentation))]public sealed class FXDepthLabPresentationEditor:FXRemainingPresentationEditor{}
}

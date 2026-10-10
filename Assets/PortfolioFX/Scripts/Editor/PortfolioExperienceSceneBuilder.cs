using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortfolioFX.Editor
{
    /// Creates the user-facing portfolio scene without touching the original
    /// showcase scene. The source scene stays useful as an isolated FX lab;
    /// the new scene is the authored end-to-end reel.
    public static class PortfolioExperienceSceneBuilder
    {
        public const string Source = "Assets/PortfolioFX/Scenes/TA_Showcase_Demo.unity";
        public const string Destination = "Assets/PortfolioFX/Scenes/TA_Portfolio_Experience.unity";

        [MenuItem("Portfolio FX/Build TA Portfolio Experience")]
        public static void Build()
        {
            if (!File.Exists(Source)) throw new FileNotFoundException("Missing source showcase scene", Source);
            if (File.Exists(Destination)) AssetDatabase.DeleteAsset(Destination);
            if (!AssetDatabase.CopyAsset(Source, Destination)) throw new IOException("Could not copy showcase scene");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
            var director = Object.FindFirstObjectByType<PortfolioShowcaseDirector>();
            if (director == null)
            {
                var root = new GameObject("TA_Portfolio_Experience_Runtime");
                director = root.AddComponent<PortfolioShowcaseDirector>();
            }
            director.autoplay = true;
            director.useVikingPrefabs = true;
            director.allowManualInput = true;
            director.routeSpeed = 1.85f;
            EditorUtility.SetDirty(director);

            var existingHud = Object.FindFirstObjectByType<PortfolioExperienceHUD>();
            if (!existingHud)
            {
                var hudRoot = new GameObject("Portfolio_Experience_Presentation");
                existingHud = hudRoot.AddComponent<PortfolioExperienceHUD>();
            }
            existingHud.director = director;
            EditorUtility.SetDirty(existingHud);

            AddBuildSetting(Destination);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("PORTFOLIO_EXPERIENCE_SCENE_READY: " + Destination);
        }

        static void AddBuildSetting(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++) if (scenes[i].path == path) { scenes[i].enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); return; }
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

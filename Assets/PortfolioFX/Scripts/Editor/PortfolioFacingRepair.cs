using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PortfolioFX.Editor
{
    public static class PortfolioFacingRepair
    {
        public const float ShowcaseYaw = 145f;
        const string Source = "Assets/UnityTechnologies/SpaceRobotKyle/Prefabs/RobotKyle.prefab";
        const string Root = PortfolioSceneBuilder.Root;

        // Only the outer character controls presentation direction. Nested model transforms
        // must retain their imported rotation, not receive the same yaw a second time.
        public static int RepairHierarchy(GameObject effect)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var idle = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "/Animations/KyleIdle.controller");
            if (source == null || idle == null) throw new InvalidOperationException("Kyle source or idle controller missing");
            int count = 0;
            foreach (var outer in effect.GetComponentsInChildren<Animator>(true))
            {
                if (outer.runtimeAnimatorController != idle) continue;
                outer.transform.localRotation = Quaternion.Euler(0, outer.name == "Caster" ? 90 : ShowcaseYaw, 0);
                foreach (var nested in outer.GetComponentsInChildren<Animator>(true))
                {
                    if (nested == outer) continue;
                    string path = AnimationUtility.CalculateTransformPath(nested.transform, outer.transform);
                    Transform original = source.transform.Find(path);
                    if (original == null) throw new InvalidOperationException("Cannot restore source model rotation: " + path);
                    nested.transform.localRotation = original.localRotation;
                    EditorUtility.SetDirty(nested.transform);
                }
                EditorUtility.SetDirty(outer.transform);
                count++;
            }
            return count;
        }

        public static void RepairAndVerify()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string backup = "RepairBackups/PortfolioFacing_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup);
            var report = new StringBuilder("# Character facing repair\n\nNested Kyle model rotations restored from the original prefab. Only the outer character is oriented toward the presentation camera; Caster keeps the firing direction. No materials, clips or camera transforms changed.\n\n");
            foreach (string path in Directory.GetFiles(Root + "/Scenes", "*.unity"))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var driver = Object.FindFirstObjectByType<FXSequenceDriver>();
                if (driver == null) continue;
                File.Copy(path, backup + "/" + Path.GetFileName(path));
                int count = RepairHierarchy(driver.gameObject);
                if (count == 0) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.AppendLine("- " + Path.GetFileName(path) + ": repaired " + count + " character(s).");
            }
            foreach (string path in Directory.GetFiles(Root + "/Prefabs", "*_FX.prefab"))
            {
                File.Copy(path, backup + "/" + Path.GetFileName(path));
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int count = RepairHierarchy(contents);
                    if (count > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            report.AppendLine("\nOriginal scene and prefab copies: `" + backup + "`.");
            File.WriteAllText(Root + "/Verification/FacingRepair.md", report.ToString(), new UTF8Encoding(false));
            AssetDatabase.SaveAssets();
            PortfolioSceneBuilder.ValidateAndCapture();
            PortfolioPlayModeCheck.RunBatch();
        }
    }
}

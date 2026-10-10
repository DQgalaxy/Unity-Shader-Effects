using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PortfolioFX.EditorTools
{
    public static class CrystalCollapseSetup
    {
        const string FX = "Assets/PortfolioFX/";
        const string ScenePath = FX + "Scenes/CrystalCollapse.unity";

        [MenuItem("Portfolio FX/Setup Crystal Collapse Presentation")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before configuring the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var portraitMesh = Require<Mesh>("Models/CrystalDiamond/CrystalPortrait.asset");
            var diamondMesh = Require<Mesh>("Models/CrystalDiamond/DiamondPresentation.asset");
            var coreMaterial = Require<Material>("MyMaterial/M_CrystalCore.mat");
            var frontMaterial = Require<Material>("MyMaterial/M_CrystalShard.mat");
            var backMaterial = Require<Material>("MyMaterial/M_CrystalDiamondBack.mat");
            var clip = Require<AnimationClip>("Animations/CrystalCollapse_EllenPresentation.anim");
            var ellen = Require<GameObject>("Characters/Ellen/EllenPortraitModel.prefab");
            string backup = "RepairBackups/CrystalEllenSetup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, backup + "/CrystalCollapse.unity");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("CrystalCollapse_FX");
            if (root == null) throw new InvalidOperationException("CrystalCollapse_FX is missing.");
            var driver = root.GetComponent<FXSequenceDriver>();
            if (driver == null) throw new InvalidOperationException("FXSequenceDriver is missing.");
            var core = root.transform.Find("CrystalCore");
            if (core == null) throw new InvalidOperationException("CrystalCore is missing.");
            core.GetComponent<MeshFilter>().sharedMesh = portraitMesh;
            core.GetComponent<MeshRenderer>().sharedMaterial = coreMaterial;
            for (int i = 0; i < 12; i++)
            {
                var shard = root.transform.Find($"Shard_{i:00}");
                if (shard == null) throw new InvalidOperationException($"Shard_{i:00} is missing.");
                shard.GetComponent<MeshFilter>().sharedMesh = diamondMesh;
                // Extra material slots render the last submesh again, back first and front second.
                shard.GetComponent<MeshRenderer>().sharedMaterials = new[] { backMaterial, frontMaterial };
            }
            var portrait = root.GetComponent<FXCrystalEllenPortrait>();
            if (portrait == null) portrait = root.AddComponent<FXCrystalEllenPortrait>();
            portrait.enabled = false;
            portrait.sequence = driver;
            portrait.crystal = core.GetComponent<Renderer>();
            portrait.ellenPrefab = ellen;
            portrait.ellenMaterial = Require<Material>("Characters/Ellen/Materials/Ellen_Base.mat");
            portrait.idle = Require<AnimationClip>("Characters/Ellen/Animations/Ellen_Idle.anim");
            portrait.action = Require<AnimationClip>("Characters/Ellen/Animations/EllenRandomIdle_1.anim");
            portrait.resolution = 512;
            portrait.enabled = true;
            driver.sequence = clip;
            driver.duration = clip.length;
            driver.playOnStart = true;
            driver.loop = true;
            driver.materials = new[]
            {
                new FXMaterialBinding { target = portrait.crystal, previewMode = FXPreviewMode.Dissolve, previewPlaceholder = false },
                new FXMaterialBinding { target = root.transform.Find("CoreDischarge").GetComponent<Renderer>(), previewMode = FXPreviewMode.Energy, previewPlaceholder = false }
            };
            driver.ResetSequence();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            PrefabUtility.SaveAsPrefabAsset(root, FX + "Prefabs/CrystalCollapse_FX.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("CrystalCollapse: Ellen portrait, reference diamond shading and ten-second sequence configured.");
        }

        static T Require<T>(string relativePath) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(FX + relativePath);
            if (asset == null) throw new InvalidOperationException("Missing asset: " + FX + relativePath);
            return asset;
        }
    }
}

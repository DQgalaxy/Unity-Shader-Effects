using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PortfolioFX.EditorTools
{
    public static class CryoPresentationVerification
    {
        [MenuItem("Portfolio FX/Verify Cryo Presentation")]
        public static void Run()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene("Assets/PortfolioFX/Scenes/CryoFreeze.unity");
                var fx = GameObject.Find("CryoFreeze_FX").GetComponent<FXCryoPresentation>();
                if (fx == null || fx.iceMeshes.Length != 4 || fx.studioReflection == null) throw new Exception("Missing ice meshes or studio cubemap.");
                foreach (var mesh in fx.iceMeshes) if (mesh == null || mesh.vertexCount == 0) throw new Exception("Ice model subasset did not resolve.");
                foreach (var mat in new[] { fx.clearIce, fx.frostedIce })
                {
                    if (mat == null || mat.shader == null || mat.GetTexture("_BumpMap") == null) throw new Exception("Ice material or normal map did not resolve.");
                    foreach (var message in ShaderUtil.GetShaderMessages(mat.shader))
                        if (message.severity.ToString() == "Error") throw new Exception(message.message);
                }
                Directory.CreateDirectory("Logs/CryoVerification");
                foreach (float t in new[] { 0f, 1.4f, 3.3f, 4.8f, 5.9f })
                {
                    fx.sequence.Seek(t); fx.SendMessage("LateUpdate");
                    var layer = fx.transform.Find("CryoPresentationLayers");
                    if (layer == null) throw new Exception("Presentation layer was not built.");
                    int visible = 0;
                    foreach (var renderer in layer.GetComponentsInChildren<Renderer>()) if (renderer.enabled) visible++;
                    if ((t == 0 || t == 5.9f) && visible != 0) throw new Exception("Crystals remain visible at sequence endpoint.");
                    if (t == 3.3f && visible != 16) throw new Exception("Expected sixteen grown ground shards.");
                    foreach (var r in fx.legacyCrystals) if (r.enabled) throw new Exception("Legacy crystal is still visible.");
                    Capture(fx.presentationCamera, $"Logs/CryoVerification/Cryo_{t:0.00}.png");
                }
                fx.sequence.Seek(0); fx.SendMessage("LateUpdate");
                File.WriteAllText("Logs/CryoVerification/result.txt", "PASS: imported model references, materials, cubemap, five rendered frames, hidden endpoints and sixteen grown ground shards.");
                Debug.Log("Cryo verification passed: Logs/CryoVerification");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void Capture(Camera camera, string path)
        {
            if (camera == null) throw new Exception("Presentation camera is missing.");
            var rt = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                rt.Create(); camera.targetTexture = rt;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
                var pixels = image.GetPixels32(); int differing = 0;
                for (int i = 1; i < pixels.Length; i += 103) if (!pixels[i].Equals(pixels[0])) differing++;
                if (differing < 100) throw new Exception("Camera image is blank.");
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}

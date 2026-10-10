using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PortfolioFX.EditorTools
{
    public static class ShieldPresentationVerification
    {
        [MenuItem("Portfolio FX/Verify Shield Presentation")]
        public static void Run()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (string name in new[] { "SG_ReactiveShield_HexSequence", "SG_ShieldImpactLayers" })
                {
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>($"Assets/PortfolioFX/Shaders/{name}.shadergraph");
                    if (shader == null) throw new Exception("Shader import failed: " + name);
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        if (message.severity.ToString() == "Error") throw new Exception(name + ": " + message.message);
                }
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene("Assets/PortfolioFX/Scenes/ReactiveShield.unity");
                var root = GameObject.Find("ReactiveShield_FX");
                var driver = root.GetComponent<FXSequenceDriver>();
                var layers = root.GetComponent<FXShieldPresentation>();
                if (layers.honeycombPrefab == null) throw new Exception("13Hexagon model reference did not resolve.");
                Directory.CreateDirectory("Logs/ShieldVerification");
                foreach (float t in new[] { 0f, .35f, .7f, 1.05f, 1.35f, 1.4f, 1.57f, 1.85f, 3.72f, 5.82f, 7.3f })
                {
                    driver.Seek(t);
                    layers.SendMessage("LateUpdate");
                    Capture(layers.presentationCamera, $"Logs/ShieldVerification/Shield_{t:0.00}.png");
                }
                driver.ResetSequence();
                layers.SendMessage("LateUpdate");
                foreach (var shaderName in new[] { "SG_ReactiveShield_HexSequence", "SG_ShieldImpactLayers" })
                {
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>($"Assets/PortfolioFX/Shaders/{shaderName}.shadergraph");
                    foreach (var m in ShaderUtil.GetShaderMessages(shader))
                        if (m.severity.ToString() == "Error") throw new Exception(m.message);
                }
                File.WriteAllText("Logs/ShieldVerification/result.txt", "PASS: imported model; compiled shader variants; rendered eleven sequence frames including reveal.");
                Debug.Log("Shield verification passed: Logs/ShieldVerification");
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
            var texture = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var readback = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                texture.Create();camera.targetTexture = texture;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;readback.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);readback.Apply();
                var pixels = readback.GetPixels32();int differing = 0;
                for (int i = 1; i < pixels.Length; i += 103) if (!pixels[i].Equals(pixels[0])) differing++;
                if (differing < 100) throw new Exception("Camera capture is blank: " + path);
                File.WriteAllBytes(path, readback.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;RenderTexture.active = previousActive;
                texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(readback);
            }
        }
    }
}

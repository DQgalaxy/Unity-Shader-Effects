using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PortfolioFX.EditorTools
{
    public static class ElectricPresentationVerification
    {
        [MenuItem("Portfolio FX/Verify Electric Presentation")]
        public static void Run()
        {
            Scene preview = default;
            const string output = "Logs/ElectricUpgrade";
            Directory.CreateDirectory(output);
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                preview = EditorSceneManager.OpenPreviewScene("Assets/PortfolioFX/Scenes/ElectricOverload.unity");
                var all = preview.GetRootGameObjects();
                var fx = all.SelectMany(r => r.GetComponentsInChildren<FXElectricPresentation>()).Single();
                var camera = fx.presentationCamera;
                camera.scene = preview;
                fx.sequence.Pause();
                foreach (float t in new[] { 0f, 1.2f, 2.1f, 3f, 4.4f, 5.9f })
                {
                    fx.sequence.Seek(t); fx.Refresh();
                    foreach (var r in fx.legacyRenderers) if (r.enabled) throw new Exception("Old pylon remains visible.");
                    var layers = fx.transform.Find("ElectricPresentationLayers");
                    if (layers == null) throw new Exception("Missing presentation layer.");
                    foreach (var r in layers.GetComponentsInChildren<Renderer>())
                    {
                        if (r.sharedMaterial == null || r.sharedMaterial.shader == null) throw new Exception("Missing material on " + r.name);
                        foreach (var msg in ShaderUtil.GetShaderMessages(r.sharedMaterial.shader))
                            if (msg.severity.ToString() == "Error") throw new Exception(msg.message);
                        if ((t == 0 || t > 5.4f) && (r.name.StartsWith("BodyBranch") || r.name.StartsWith("BodyContact")) && r.enabled)
                            throw new Exception("Body discharge survived reset.");
                    }
                    foreach (var mat in new[] { fx.body.sharedMaterial, fx.sequence.arcs[0].GetComponent<Renderer>().sharedMaterial })
                        foreach (var msg in ShaderUtil.GetShaderMessages(mat.shader))
                            if (msg.severity.ToString() == "Error") throw new Exception(msg.message);
                    Capture(camera, $"{output}/Electric_{t:0.00}.png");
                }
                fx.sequence.Seek(2.1f); fx.Refresh();
                Capture(camera, output + "/Electric_Repeat.png");
                if (!File.ReadAllBytes(output + "/Electric_2.10.png").SequenceEqual(File.ReadAllBytes(output + "/Electric_Repeat.png")))
                    throw new Exception("Repeat seek produced a different image; inspect particles and render temporal effects.");
                File.WriteAllText(output + "/unity-result.txt", "PASS: shader imports; six rendered frames; reset; deterministic repeat seek. No user scene was saved or closed.");
                Debug.Log("Electric verification passed: " + output);
            }
            catch (Exception e)
            {
                File.WriteAllText(output + "/unity-result.txt", "FAIL: " + e);
                Debug.LogException(e);
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static void Capture(Camera camera, string path)
        {
            var rt = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGBHalf);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var image = new Texture2D(1280,800,TextureFormat.RGB24,false);
            try
            {
                rt.Create(); camera.targetTexture = rt;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; image.ReadPixels(new Rect(0,0,1280,800),0,0); image.Apply();
                var pixels = image.GetPixels32(); int differing=0, magenta=0;
                for(int i=1;i<pixels.Length;i+=43)
                {
                    if (!pixels[i].Equals(pixels[0])) differing++;
                    if(pixels[i].r>230 && pixels[i].b>230 && pixels[i].g<30) magenta++;
                }
                if(differing<100 || magenta>100) throw new Exception("Blank camera image or error-shader pixels.");
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=previous; RenderTexture.active=active;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}

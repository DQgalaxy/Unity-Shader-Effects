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
    public static class HologramPresentationVerification
    {
        [MenuItem("Portfolio FX/Verify Hologram Presentation")]
        public static void Run()
        {
            const string output = "Logs/HologramUpgrade";
            Directory.CreateDirectory(output);
            Scene preview = default;
            bool success = false;
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                preview = EditorSceneManager.OpenPreviewScene("Assets/PortfolioFX/Scenes/HologramMaterialize.unity");
                var roots = preview.GetRootGameObjects();
                var driver = roots.SelectMany(o => o.GetComponentsInChildren<FXSequenceDriver>()).Single();
                var camera = roots.SelectMany(o => o.GetComponentsInChildren<Camera>()).First();
                camera.scene = preview;
                var body = driver.GetComponentsInChildren<SkinnedMeshRenderer>().Single(o => o.name == "Ellen_Body");
                var pivot = driver.transform.Find("HologramBody");
                var scan = driver.transform.Find("ScanRing").GetComponent<Renderer>();
                var beam = driver.transform.Find("TransportBeam").GetComponent<MeshRenderer>();
                if (body.sharedMaterial.renderQueue != 2450) throw new Exception("Body must use cutout depth rendering.");
                if (beam.sharedMaterial == null || beam.GetComponent<MeshFilter>().sharedMesh == null) throw new Exception("Missing transport resources.");
                if (driver.duration != 10 || driver.sequence == null) throw new Exception("Wrong sequence duration or missing clip.");
                foreach (string property in new[] { "_BaseMap", "_NormalMap", "_MetallicSmoothMap", "_OcclusionMap", "_SignalNoise" })
                    if (body.sharedMaterial.GetTexture(property) == null) throw new Exception("Missing " + property);
                var bindings = AnimationUtility.GetCurveBindings(driver.sequence);
                if (!bindings.Any(b => b.path == "HologramBody" && (b.propertyName.Contains("Euler") || b.propertyName.Contains("Rotation"))))
                    throw new Exception("Turntable rotation curve did not import.");
                driver.Pause();
                Quaternion first = Quaternion.identity;
                Vector3 position = pivot.localPosition;
                foreach (float t in new[] { 0f, 1.45f, 2.5f, 2.8f, 4f, 5.3f, 6.5f, 7.8f, 8.8f, 10f })
                {
                    driver.Seek(t);
                    if (scan.enabled) throw new Exception("Second scan ring re-enabled.");
                    if (Vector3.Distance(pivot.localPosition, position) > .001f) throw new Exception("Character translation instead of in-place turntable.");
                    if (t == 2.8f) first = pivot.localRotation;
                    if (t == 5.3f && Quaternion.Angle(first,pivot.localRotation) < 170) throw new Exception("Expected halfway turn to show character back.");
                    if (t == 7.8f && Quaternion.Angle(first,pivot.localRotation) > 1) throw new Exception("Full turn did not return to front.");
                    var block = new MaterialPropertyBlock(); body.GetPropertyBlock(block);
                    if ((t == 0 || t == 10) && block.GetFloat("_Progress") > .001f) throw new Exception("Reveal survived reset.");
                    if (t >= 2.5f && t <= 7.8f && block.GetFloat("_Progress") < .999f) throw new Exception("Character faded during turntable.");
                    Capture(camera, $"{output}/Hologram_{t:0.00}.png");
                    foreach (var shader in new[] {body.sharedMaterial.shader, beam.sharedMaterial.shader})
                        foreach (var message in ShaderUtil.GetShaderMessages(shader))
                            if (message.severity.ToString() == "Error") throw new Exception(message.message);
                }
                driver.Seek(1.45f); var q = pivot.localRotation;
                driver.Seek(7.8f); driver.Seek(1.45f);
                if (Quaternion.Angle(q,pivot.localRotation) > .001f) throw new Exception("Rotation accumulates on repeated seeks.");
                File.WriteAllText(output + "/unity-result.txt", "PASS: imported detail maps and transport mesh, both shader compilations, reveal timing, in-place full rotation, rewind and ten rendered frames. User scenes not modified. Inspect captures for self-overlap and scan appearance.");
                success = true;
                Debug.Log("Hologram verification passed: " + output);
            }
            catch (Exception e)
            {
                File.WriteAllText(output + "/unity-result.txt", "FAIL: " + e);
                Debug.LogException(e);
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            }
        }

        static void Capture(Camera camera, string path)
        {
            var rt = new RenderTexture(1280,800,24,RenderTextureFormat.ARGBHalf);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var image = new Texture2D(1280,800,TextureFormat.RGB24,false);
            try
            {
                rt.Create(); camera.targetTexture = rt;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; image.ReadPixels(new Rect(0,0,1280,800),0,0); image.Apply();
                var pixels = image.GetPixels32(); int different = 0, magenta = 0;
                for(int i=1;i<pixels.Length;i+=43)
                {
                    if (!pixels[i].Equals(pixels[0])) different++;
                    if (pixels[i].r>230 && pixels[i].b>230 && pixels[i].g<30) magenta++;
                }
                if (different<100 || magenta>100) throw new Exception("Blank frame or error shader pixels.");
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}

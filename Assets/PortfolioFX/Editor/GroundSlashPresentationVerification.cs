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
    public static class GroundSlashPresentationVerification
    {
        [MenuItem("Portfolio FX/Verify Ground Slash Presentation")]
        public static void Run()
        {
            const string output = "Logs/GroundSlashScythe";
            Directory.CreateDirectory(output);
            Scene scene = default;
            bool passed = false;
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                scene = EditorSceneManager.OpenPreviewScene("Assets/PortfolioFX/Scenes/GroundSlash.unity");
                var fx = scene.GetRootGameObjects().SelectMany(g =>
                    g.GetComponentsInChildren<FXGroundSlashLesson>()).Single();
                if (fx.ConfigurationError != null) throw new Exception(fx.ConfigurationError);
                if (fx.hand.name != "Ellen_Left_Hand") throw new Exception("Release must use Ellen_Left_Hand.");
                fx.viewCamera.scene = scene;
                fx.sequence.Pause();
                foreach (float t in new[] { 0f, .5f, .62f, .7f, .85f, 1.06f, 1.22f, 1.6f, 3.8f, .85f })
                {
                    fx.sequence.Seek(t); fx.Refresh();
                    var visuals = fx.transform.Find("GroundSlashLesson_Generated");
                    if (visuals.GetComponentsInChildren<Transform>().Any(x => x.name.StartsWith("Afterimage_")))
                        throw new Exception("Duplicate crescent afterimages remain.");
                    int blades = new[] { "PalmBlade", "FlyingBlade" }.Count(n =>
                        visuals.Find(n).GetComponent<Renderer>().enabled);
                    int expected = (t > .18f && t < fx.impactTime + .15f) ? 1 : 0;
                    if (blades != expected) throw new Exception("Single-blade visibility mismatch at " + t);
                    if (Mathf.Approximately(t, fx.releaseTime))
                    {
                        var flying = visuals.Find("FlyingBlade");
                        if (Mathf.Abs(flying.localScale.x - fx.releaseSize) > .001f)
                            throw new Exception("Blade unfolded before clearing the hand.");
                        Vector3 forward = Vector3.ProjectOnPlane(fx.hit.position - fx.hand.position, Vector3.up).normalized;
                        Vector3 expectedPosition = fx.hand.position + forward * fx.handForwardOffset + Vector3.up * .04f;
                        if (Vector3.Distance(flying.position, expectedPosition) > .001f)
                            throw new Exception("Release position did not follow the left hand and forward offset.");
                    }
                    if (Mathf.Approximately(t, .7f) && visuals.Find("FlyingBlade").localScale.x >= 1)
                        throw new Exception("Blade grew too early near the character.");
                    foreach (var v in visuals.Find("FlyingBlade").GetComponent<MeshFilter>().sharedMesh.vertices)
                        if (!float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z))
                            throw new Exception("Non-finite sweep geometry.");
                    if (Mathf.Approximately(t, 1.06f) && fx.sparks.particleCount == 0)
                        throw new Exception("No impact sparks after the burst.");
                    if (t >= 3.8f && (fx.sparks.particleCount + fx.debris.particleCount + fx.dust.particleCount > 0))
                        throw new Exception("Particles survived the reset interval.");
                    foreach (var r in visuals.GetComponentsInChildren<Renderer>())
                        foreach (var mat in r.sharedMaterials.Where(m => m != null))
                            foreach (var msg in ShaderUtil.GetShaderMessages(mat.shader))
                                if (msg.severity.ToString() == "Error") throw new Exception(msg.message);
                    Capture(fx.viewCamera, output + "/Scythe_" + t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png");
                }
                File.WriteAllText(output + "/unity-result.txt", "PASS: one blade, no crescent duplicates, finite mesh, impact sparks, loop cleanup and shader import; inspect captured images for visual quality.");
                passed = true;
            }
            catch (Exception e) { File.WriteAllText(output + "/unity-result.txt", "FAIL: " + e); Debug.LogException(e); }
            finally
            {
                if (scene.IsValid())
                {
                    foreach (var m in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FXCharacterMotion>())) m.ReleaseGraph();
                    EditorSceneManager.ClosePreviewScene(scene);
                }
                if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        static void Capture(Camera camera, string path)
        {
            var rt = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGBHalf);
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                rt.Create(); camera.targetTexture = rt;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}

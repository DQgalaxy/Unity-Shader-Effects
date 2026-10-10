using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PortfolioFX.EditorTools
{
    // Explicit, opt-in capture tool. Never saves or rebuilds source scenes.
    // Run from the menu or -executeMethod without -quit / -nographics.
    [InitializeOnLoad]
    public static class PortfolioReadmeCapture
    {
        const string Active = "PortfolioFX.ReadmeCapture.Active";
        const string IndexKey = "PortfolioFX.ReadmeCapture.Index";
        const int Fps = 15;
        const int Width = 800, Height = 500;
        static readonly string[] Names = { "ReactiveShield", "ArcaneChargeShot", "GroundSlash", "CryoFreeze", "Overheat", "ElectricOverload", "HologramMaterialize", "CrystalCollapse", "MeteorStrike", "WaterShockwave", "DepthIntersectionLab" };
        static readonly float[] HeroTimes = { 1.72f, 2.4f, .85f, 3.3f, 2.8f, 2.7f, 3.1f, 2.8f, 2.5f, 1.8f, 1.5f };
        static int index, frame, previousFrame;
        static float previousTime, nextCapture, bestHeroScore;
        static double started;
        static bool ready, heroCaptured, hooked;
        static FXSequenceDriver driver;
        static Camera camera;
        static RenderTexture rt;
        static Texture2D image;
        static readonly List<string> errors = new List<string>();

        [Serializable] sealed class Result
        {
            public string scene, status, checkedUtc;
            public int frames, fps;
            public float duration;
            public string[] errors;
        }

        static PortfolioReadmeCapture()
        {
            if (SessionState.GetBool(Active, false)) { index = SessionState.GetInt(IndexKey, 0); Subscribe(); }
        }

        [MenuItem("Portfolio FX/Capture README Gallery")]
        public static void Run()
        {
            if (!Application.isBatchMode && (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())) return;
            Directory.CreateDirectory("Docs/media");
            Directory.CreateDirectory("Docs/verification");
            Directory.CreateDirectory("Logs/ReadmeCaptureBright");
            SessionState.SetBool(Active, true);
            SessionState.SetInt(IndexKey, 0);
            index = 0;
            Subscribe();
            OpenCurrent();
            EditorApplication.EnterPlaymode();
        }

        static void Subscribe()
        {
            if (hooked) return;
            hooked = true;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        static void OpenCurrent()
        {
            ready = heroCaptured = false;
            frame = 0; previousFrame = -1; previousTime = -1; nextCapture = 0;
            bestHeroScore = -1;
            driver = null; camera = null; errors.Clear(); started = EditorApplication.timeSinceStartup;
            string path = "Assets/PortfolioFX/Scenes/" + Names[index] + ".unity";
            if (EditorApplication.isPlaying) EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            else EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Directory.CreateDirectory("Logs/ReadmeCaptureBright/" + Names[index]);
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (errors.Count < 30) errors.Add(type + ": " + message + "\n" + stack);
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (!EditorApplication.isPlaying) return;
            if (SceneManager.GetActiveScene().name != Names[index]) return;
            if (!ready)
            {
                driver = Object.FindFirstObjectByType<FXSequenceDriver>();
                camera = Camera.main;
                if (!driver || !camera)
                {
                    if (EditorApplication.timeSinceStartup - started > 30) { errors.Add("Missing FXSequenceDriver or main camera."); FinishScene(); }
                    return;
                }
                Time.captureDeltaTime = 1f / Fps;
                ConfigurePresentationLighting();
                driver.playbackSpeed = 1; driver.loop = true; driver.Play();
                foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go.gameObject) > 0) errors.Add("Missing script on " + go.name);
                ready = true; started = EditorApplication.timeSinceStartup;
                return;
            }
            float t = driver.CurrentTime;
            if (!float.IsFinite(t) || !float.IsFinite(driver.intensity)) { errors.Add("Nonfinite sequence parameter."); FinishScene(); return; }
            if (previousTime > driver.duration * .8f && t + .01f < previousTime)
            {
                if (frame < Fps || !heroCaptured) errors.Add("Insufficient runtime frames or no hero capture.");
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    foreach (var mat in r.sharedMaterials.Where(m => m != null))
                        foreach (var msg in ShaderUtil.GetShaderMessages(mat.shader))
                            if (msg.severity.ToString() == "Error" && errors.Count < 30) errors.Add(mat.shader.name + ": " + msg.message);
                FinishScene(); return;
            }
            if (Time.frameCount == previousFrame) return;
            previousFrame = Time.frameCount;
            if (t >= nextCapture)
            {
                try
                {
                    Capture("Logs/ReadmeCaptureBright/" + Names[index] + "/frame_" + frame.ToString("D4") + ".png", t);
                    frame++; nextCapture = t + .8f / Fps;
                }
                catch (Exception ex) { errors.Add("Capture: " + ex.Message); FinishScene(); return; }
            }
            previousTime = t;
            if (EditorApplication.timeSinceStartup - started > 120) { errors.Add("Playback timeout."); FinishScene(); }
        }

        static void ConfigurePresentationLighting()
        {
            // Capture-only studio lighting. Effect assets and source scenes are never saved.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.16f, .18f, .21f, 1);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.62f, .65f, .7f);
            RenderSettings.ambientEquatorColor = new Color(.38f, .42f, .46f);
            RenderSettings.ambientGroundColor = new Color(.2f, .23f, .26f);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.name == "KeyLight") light.intensity *= 1.8f;
                else if (light.name == "RimLight") light.intensity *= 1.5f;
            }
        }

        static void Capture(string path, float time)
        {
            // An sRGB target preserves the display transfer when PNG bytes are read back.
            // ARGBHalf has no sRGB encoding and made the previous exports much too dark.
            if (!rt) { rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { hideFlags = HideFlags.HideAndDontSave }; rt.Create(); }
            if (!image) image = new Texture2D(Width, Height, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply();
                var bytes = image.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
                if (Mathf.Abs(time - HeroTimes[index]) <= .45f)
                {
                    var pixels = image.GetPixels32(); var first = pixels[0]; int varied = 0, magenta = 0;
                    for (int i = 0; i < pixels.Length; i += 8)
                    {
                        var p = pixels[i];
                        if (Math.Abs(p.r - first.r) + Math.Abs(p.g - first.g) + Math.Abs(p.b - first.b) > 25) varied++;
                        if (p.r > 220 && p.b > 220 && p.g < 50) magenta++;
                    }
                    int samples = (pixels.Length + 7) / 8;
                    if (magenta > samples * .01f) throw new Exception("Possible error shader pixels.");
                    if (varied > samples * .025f && varied > bestHeroScore)
                    {
                        bestHeroScore = varied;
                        File.WriteAllBytes("Docs/media/" + Names[index] + ".png", bytes);
                        heroCaptured = true;
                    }
                }
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; }
        }

        static void FinishScene()
        {
            var result = new Result { scene = Names[index], status = errors.Count == 0 ? "PASS" : "FAIL", checkedUtc = DateTime.UtcNow.ToString("O"), frames = frame, fps = Fps, duration = driver ? driver.duration : 0, errors = errors.ToArray() };
            File.WriteAllText("Docs/verification/" + Names[index] + ".json", JsonUtility.ToJson(result, true));
            Debug.Log("README_CAPTURE_" + result.status + ": " + result.scene + " / " + frame + " frames");
            index++;
            SessionState.SetInt(IndexKey, index);
            if (index < Names.Length) { OpenCurrent(); return; }
            SessionState.SetBool(Active, false);
            EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; hooked = false;
            Time.captureDeltaTime = 0;
            if (rt) { rt.Release(); Object.DestroyImmediate(rt); }
            if (image) Object.DestroyImmediate(image);
            if (Application.isBatchMode) EditorApplication.Exit(0);
            else EditorApplication.ExitPlaymode();
        }
    }
}

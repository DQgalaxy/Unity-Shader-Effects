using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PortfolioFX.Editor
{
    [InitializeOnLoad]
    public static class PortfolioPlayModeCheck
    {
        const string Flag = "PortfolioFX.AutomatedPlayCheck";
        static readonly string[] Names = { "Overheat", "CrystalCollapse", "ReactiveShield", "CryoFreeze", "ElectricOverload", "HologramMaterialize", "ArcaneChargeShot", "GroundSlash", "MeteorStrike", "FoldstepPortal", "WaterShockwave", "DepthIntersectionLab" };
        static int index, wraps;
        static float previous;
        static double started;
        static FXSequenceDriver driver;
        static bool captured, failed, running;
        static StringBuilder report;

        static PortfolioPlayModeCheck()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        public static void RunBatch()
        {
            PortfolioSceneBuilder.ExportHandbook();
            SessionState.SetBool(Flag, true);
            EditorSceneManager.OpenScene(PortfolioSceneBuilder.Root + "/Scenes/Overheat.unity");
            EditorApplication.EnterPlaymode();
        }

        static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Flag, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                report = new StringBuilder("# Play Mode smoke test\n\nReal PlayerLoop, accelerated 4x, three observed wraps per scene.\n\n");
                index = 0; failed = false; running = true; Prepare();
            }
        }

        static void Prepare()
        {
            driver = Object.FindFirstObjectByType<FXSequenceDriver>();
            if (driver == null) { Fail("Missing runtime driver"); return; }
            driver.playbackSpeed = 4; driver.loop = true; driver.Play();
            wraps = 0; previous = 0; captured = false; started = EditorApplication.timeSinceStartup;
        }

        static void Tick()
        {
            if (!running || failed || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup - started > 45) { Fail("Runtime progression timeout in " + Names[index]); return; }
            if(driver==null)
            {
                if(SceneManager.GetActiveScene().name==Names[index]) Prepare();
                return;
            }
            float time = driver.CurrentTime;
            if (!float.IsFinite(time) || !float.IsFinite(driver.intensity)) { Fail("Non-finite runtime parameter"); return; }
            if (time + .01f < previous) wraps++;
            previous = time;
            if (!captured && time > driver.duration * .4f && wraps == 0)
            {
                Capture(PortfolioSceneBuilder.Root + "/Verification/" + Names[index] + "_PlayMode.png");
                captured = true;
            }
            if (wraps < 3) return;
            driver.Pause(); driver.Seek(driver.duration * .5f);
            driver.ResetSequence();
            foreach (var cue in driver.particles)
                if (cue.system != null && cue.start > 0 && cue.system.particleCount != 0) { Fail("Particles survived reset: " + cue.system.name); return; }
            report.AppendLine("- PASS `" + Names[index] + "`: three loops, runtime screenshot, pause/seek/reset; delayed particles cleared.");
            index++;
            if (index == Names.Length)
            {
                File.WriteAllText(PortfolioSceneBuilder.Root + "/Verification/PlayModeReport.md", report.ToString(), new UTF8Encoding(false));
                running = false; SessionState.SetBool(Flag, false); Debug.Log("PORTFOLIO_PLAYMODE_SUCCESS");
                EditorApplication.Exit(0); return;
            }
            driver=null; started=EditorApplication.timeSinceStartup;
            EditorSceneManager.LoadSceneInPlayMode(PortfolioSceneBuilder.Root + "/Scenes/" + Names[index] + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (running && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)) Fail(message + "\n" + stack);
        }

        static void Fail(string reason)
        {
            if (failed) return;
            failed = true; running = false; SessionState.SetBool(Flag, false);
            File.WriteAllText(PortfolioSceneBuilder.Root + "/Verification/PlayModeFailure.txt", reason);
            EditorApplication.Exit(1);
        }

        static void Capture(string path)
        {
            var camera = Camera.main;
            var rt = RenderTexture.GetTemporary(960, 600, 24);
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture = new Texture2D(960, 600, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 960, 600), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(rt); if (texture != null) Object.DestroyImmediate(texture);
            }
        }
    }
}

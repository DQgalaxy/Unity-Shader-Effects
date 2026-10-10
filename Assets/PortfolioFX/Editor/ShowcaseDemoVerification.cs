using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PortfolioFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortfolioFX.Editor
{
    /// <summary>
    /// Opt-in, batch-friendly smoke test for the authored showcase route.
    /// Invoke with -executeMethod PortfolioFX.Editor.ShowcaseDemoVerification.Run.
    /// It never runs from normal editor startup; the SessionState marker exists
    /// only between an explicit Run call and the final EditorApplication.Exit.
    /// </summary>
    [InitializeOnLoad]
    public static class ShowcaseDemoVerification
    {
        const string ActiveKey = "PortfolioFX.ShowcaseVerification.Active";
        const string StageKey = "PortfolioFX.ShowcaseVerification.Stage";
        const string OutputKey = "PortfolioFX.ShowcaseVerification.Output";
        const string SceneKey = "PortfolioFX.ShowcaseVerification.Scene";
        const float Duration = 25f;
        static readonly float[] CaptureTimes = { 2f, 5f, 8f, 11f, 14f, 19f };

        enum Stage { Idle, WaitingForPlay, Running, Stopping, Writing }

        static Stage stage;
        static string outputDirectory;
        static string scenePath;
        static double runStart;
        static bool directorReady;
        static bool metricInitialized;
        static bool errorHooked;
        static bool updateHooked;
        static bool directorMissingReported;
        static int nextCapture;
        static int fatalLogCount;
        static PortfolioShowcaseDirector director;
        static Camera captureCamera;
        static RenderTexture captureTexture;
        static Transform[] bones = Array.Empty<Transform>();
        static Quaternion[] previousBoneRotations = Array.Empty<Quaternion>();
        static float[] maxBoneRotationDelta = Array.Empty<float>();
        static readonly List<string> errors = new List<string>();
        static readonly List<string> phaseHistory = new List<string>();
        static readonly List<string> captures = new List<string>();
        static float firstCharacterY, lastCharacterY, minCharacterY = float.PositiveInfinity, maxCharacterY = float.NegativeInfinity;
        static float firstFootY, lastFootY, minFootY = float.PositiveInfinity, maxFootY = float.NegativeInfinity;
        static float firstYaw, lastYaw, yawTravel;
        static string lastPhase = string.Empty;
        static double lastMetricSample;

        static ShowcaseDemoVerification()
        {
            if (!SessionState.GetBool(ActiveKey, false))
                return;

            outputDirectory = SessionState.GetString(OutputKey, string.Empty);
            scenePath = SessionState.GetString(SceneKey, string.Empty);
            stage = (Stage)SessionState.GetInt(StageKey, (int)Stage.WaitingForPlay);
            if (stage == Stage.Idle)
                stage = EditorApplication.isPlaying ? Stage.Running : Stage.WaitingForPlay;
            Subscribe();
        }

        /// <summary>Explicit entry point for Unity's -executeMethod option.</summary>
        public static void Run()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                Debug.LogWarning("TA showcase verification is already running.");
                return;
            }

            ResetRunState();
            scenePath = FindShowcaseScene();
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("TA showcase verification could not find Assets/PortfolioFX/Scenes/TA_Showcase_Demo.unity.");
                EditorApplication.Exit(2);
                return;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            outputDirectory = Path.Combine(projectRoot, "Verification", "TA_Showcase");
            Directory.CreateDirectory(outputDirectory);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(StageKey, (int)Stage.WaitingForPlay);
            SessionState.SetString(OutputKey, outputDirectory);
            SessionState.SetString(SceneKey, scenePath);
            stage = Stage.WaitingForPlay;
            Subscribe();

            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
            catch (Exception ex)
            {
                errors.Add("OpenScene/PlayMode: " + ex);
                stage = Stage.Stopping;
                SessionState.SetInt(StageKey, (int)Stage.Stopping);
                WriteReportAndExit();
            }
        }

        static void ResetRunState()
        {
            stage = Stage.Idle;
            outputDirectory = string.Empty;
            scenePath = string.Empty;
            runStart = 0;
            directorReady = false;
            directorMissingReported = false;
            metricInitialized = false;
            nextCapture = 0;
            fatalLogCount = 0;
            director = null;
            captureCamera = null;
            captureTexture = null;
            bones = Array.Empty<Transform>();
            previousBoneRotations = Array.Empty<Quaternion>();
            maxBoneRotationDelta = Array.Empty<float>();
            errors.Clear();
            phaseHistory.Clear();
            captures.Clear();
            firstCharacterY = lastCharacterY = firstFootY = lastFootY = firstYaw = lastYaw = yawTravel = 0;
            minCharacterY = minFootY = float.PositiveInfinity;
            maxCharacterY = maxFootY = float.NegativeInfinity;
            lastPhase = string.Empty;
            lastMetricSample = 0;
        }

        static string FindShowcaseScene()
        {
            const string expected = "Assets/PortfolioFX/Scenes/TA_Showcase_Demo.unity";
            if (File.Exists(Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty, expected.Replace('/', Path.DirectorySeparatorChar))))
                return expected;
            var guids = AssetDatabase.FindAssets("TA_Showcase_Demo t:Scene");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("TA_Showcase_Demo.unity", StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return string.Empty;
        }

        static void Subscribe()
        {
            if (!updateHooked)
            {
                EditorApplication.update += EditorUpdate;
                updateHooked = true;
            }
            if (!errorHooked)
            {
                Application.logMessageReceivedThreaded += OnLogMessage;
                errorHooked = true;
            }
        }

        static void Unsubscribe()
        {
            if (updateHooked)
            {
                EditorApplication.update -= EditorUpdate;
                updateHooked = false;
            }
            if (errorHooked)
            {
                Application.logMessageReceivedThreaded -= OnLogMessage;
                errorHooked = false;
            }
        }

        static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert && type != LogType.Warning)
                return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                System.Threading.Interlocked.Increment(ref fatalLogCount);
            lock (errors)
            {
                if (errors.Count >= 160)
                    return;
                var line = type + ": " + condition;
                if (!string.IsNullOrEmpty(stackTrace))
                    line += "\n" + stackTrace.Trim();
                errors.Add(line);
            }
        }

        static void EditorUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                Unsubscribe();
                return;
            }

            if (stage == Stage.WaitingForPlay)
            {
                if (EditorApplication.isPlaying)
                {
                    stage = Stage.Running;
                    SessionState.SetInt(StageKey, (int)Stage.Running);
                    runStart = EditorApplication.timeSinceStartup;
                }
                return;
            }

            if (stage == Stage.Stopping)
            {
                if (!EditorApplication.isPlaying)
                    WriteReportAndExit();
                return;
            }

            if (stage == Stage.Running && !EditorApplication.isPlaying)
            {
                // Covers an external Stop button or a PlayMode exception.
                stage = Stage.Stopping;
                SessionState.SetInt(StageKey, (int)Stage.Stopping);
                WriteReportAndExit();
                return;
            }
            if (stage != Stage.Running)
                return;

            var elapsed = (float)(EditorApplication.timeSinceStartup - runStart);
            if (!directorReady)
            {
                director = UnityEngine.Object.FindFirstObjectByType<PortfolioShowcaseDirector>(FindObjectsInactive.Include);
                if (director)
                {
                    directorReady = true;
                    captureCamera = director.playerCamera ? director.playerCamera : Camera.main;
                    if (!captureCamera)
                        captureCamera = UnityEngine.Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
                    SetupBones();
                }
                else if (elapsed > 8f && !directorMissingReported)
                {
                    errors.Add("PortfolioShowcaseDirector was not found after 8 seconds of PlayMode.");
                    fatalLogCount++;
                    directorMissingReported = true;
                }
            }

            if (directorReady)
            {
                SampleMetrics(elapsed);
                while (nextCapture < CaptureTimes.Length && elapsed >= CaptureTimes[nextCapture])
                {
                    CaptureFrame(CaptureTimes[nextCapture]);
                    nextCapture++;
                }
            }

            if (elapsed >= Duration)
            {
                while (nextCapture < CaptureTimes.Length)
                {
                    CaptureFrame(CaptureTimes[nextCapture]);
                    nextCapture++;
                }
                stage = Stage.Stopping;
                SessionState.SetInt(StageKey, (int)Stage.Stopping);
                EditorApplication.isPlaying = false;
            }
        }

        static void SetupBones()
        {
            var player = director ? director.player : null;
            if (!player)
                return;
            var all = player.GetComponentsInChildren<Transform>(true);
            var selected = new List<Transform>();
            for (var i = 0; i < all.Length && selected.Count < 32; i++)
            {
                var t = all[i];
                if (t == player || t.name.IndexOf("visual", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                selected.Add(t);
            }
            bones = selected.ToArray();
            previousBoneRotations = new Quaternion[bones.Length];
            maxBoneRotationDelta = new float[bones.Length];
        }

        static void SampleMetrics(float elapsed)
        {
            var player = director ? director.player : null;
            if (!player)
                return;
            var y = player.position.y;
            var footY = y;
            var renderers = player.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                footY = bounds.min.y;
            }
            minCharacterY = Mathf.Min(minCharacterY, y); maxCharacterY = Mathf.Max(maxCharacterY, y); lastCharacterY = y;
            minFootY = Mathf.Min(minFootY, footY); maxFootY = Mathf.Max(maxFootY, footY); lastFootY = footY;
            var yaw = player.eulerAngles.y;
            if (!metricInitialized)
            {
                firstCharacterY = y; firstFootY = footY; firstYaw = yaw; lastYaw = yaw;
                for (var i = 0; i < bones.Length; i++) previousBoneRotations[i] = bones[i].localRotation;
                metricInitialized = true;
            }
            else
            {
                yawTravel += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
                lastYaw = yaw;
                for (var i = 0; i < bones.Length; i++)
                {
                    if (!bones[i]) continue;
                    var delta = Quaternion.Angle(previousBoneRotations[i], bones[i].localRotation);
                    maxBoneRotationDelta[i] = Mathf.Max(maxBoneRotationDelta[i], delta);
                    previousBoneRotations[i] = bones[i].localRotation;
                }
            }
            if (elapsed - lastMetricSample >= .2f || string.IsNullOrEmpty(lastPhase))
            {
                var phase = director.phase.ToString();
                if (!string.Equals(phase, lastPhase, StringComparison.Ordinal))
                {
                    phaseHistory.Add(elapsed.ToString("0.00") + "s  " + phase);
                    lastPhase = phase;
                }
                lastMetricSample = elapsed;
            }
        }

        static void CaptureFrame(float nominalTime)
        {
            if (!captureCamera)
            {
                captures.Add(nominalTime.ToString("0") + "s: camera missing");
                return;
            }
            var path = Path.Combine(outputDirectory, "TA_Showcase_" + nominalTime.ToString("00") + "s.png");
            try
            {
                if (!captureTexture)
                {
                    captureTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32)
                    {
                        name = "TA_Showcase_Verification_RT",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    captureTexture.Create();
                }
                var oldTarget = captureCamera.targetTexture;
                var oldActive = RenderTexture.active;
                captureCamera.targetTexture = captureTexture;
                captureCamera.Render();
                RenderTexture.active = captureTexture;
                var image = new Texture2D(captureTexture.width, captureTexture.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, captureTexture.width, captureTexture.height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.active = oldActive;
                captureCamera.targetTexture = oldTarget;
                captures.Add(nominalTime.ToString("0") + "s: " + path);
            }
            catch (Exception ex)
            {
                captures.Add(nominalTime.ToString("0") + "s: FAILED " + ex.Message);
            }
        }

        static void WriteReportAndExit()
        {
            if (stage == Stage.Writing)
                return;
            stage = Stage.Writing;
            SessionState.SetInt(StageKey, (int)Stage.Writing);
            try
            {
                if (captureTexture)
                {
                    captureTexture.Release();
                    UnityEngine.Object.DestroyImmediate(captureTexture);
                    captureTexture = null;
                }
                var sb = new StringBuilder();
                sb.AppendLine("TA Showcase Verification Report");
                sb.AppendLine("Generated UTC: " + DateTime.UtcNow.ToString("O"));
                sb.AppendLine("Scene: " + scenePath);
                sb.AppendLine("Duration target: " + Duration.ToString("0.0") + " seconds");
                sb.AppendLine("Director found: " + directorReady);
                sb.AppendLine("Errors/warnings captured: " + errors.Count);
                sb.AppendLine("Fatal error/exception/assert count: " + fatalLogCount);
                sb.AppendLine();
                sb.AppendLine("Phase timeline:");
                foreach (var item in phaseHistory) sb.AppendLine("  " + item);
                sb.AppendLine();
                sb.AppendLine("Character metrics:");
                sb.AppendLine("  World Y first/last/min/max: " + Format(firstCharacterY) + "/" + Format(lastCharacterY) + "/" + Format(minCharacterY) + "/" + Format(maxCharacterY));
                sb.AppendLine("  Renderer foot Y first/last/min/max: " + Format(firstFootY) + "/" + Format(lastFootY) + "/" + Format(minFootY) + "/" + Format(maxFootY));
                sb.AppendLine("  Yaw first/last/travel: " + Format(firstYaw) + "/" + Format(lastYaw) + "/" + Format(yawTravel) + " degrees");
                var changedBones = 0;
                for (var i = 0; i < maxBoneRotationDelta.Length; i++) if (maxBoneRotationDelta[i] > .5f) changedBones++;
                sb.AppendLine("  Animation bones tracked/changed: " + bones.Length + "/" + changedBones);
                for (var i = 0; i < bones.Length; i++)
                    sb.AppendLine("    " + (bones[i] ? bones[i].name : "<destroyed>") + ": max local rotation delta " + Format(maxBoneRotationDelta[i]) + " degrees");
                sb.AppendLine();
                sb.AppendLine("Screenshots:");
                foreach (var item in captures) sb.AppendLine("  " + item);
                sb.AppendLine();
                sb.AppendLine("Captured log messages:");
                lock (errors) foreach (var item in errors) sb.AppendLine("  " + item.Replace("\n", "\n  "));
                File.WriteAllText(Path.Combine(outputDirectory, "TA_Showcase_Verification.txt"), sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(outputDirectory, "TA_Showcase_Verification_FAILED.txt"), ex.ToString(), Encoding.UTF8); } catch { }
            }
            SessionState.SetBool(ActiveKey, false);
            Unsubscribe();
            AssetDatabase.Refresh();
            EditorApplication.Exit(fatalLogCount > 0 ? 2 : 0);
        }

        static string Format(float value)
        {
            return float.IsInfinity(value) || float.IsNaN(value) ? "n/a" : value.ToString("0.000");
        }
    }
}

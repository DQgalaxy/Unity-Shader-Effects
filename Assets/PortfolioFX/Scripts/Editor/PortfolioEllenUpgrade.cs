using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PortfolioFX.Editor
{
    public static class PortfolioEllenUpgrade
    {
        const string Root = PortfolioSceneBuilder.Root;
        const string Source = "F:/UnityProjects/OthersDemo/VisualEffectGraph-Samples-master/Assets/Samples/Common/3DGameKitCharacters";
        public const string CharacterRoot = Root + "/Characters/Ellen";
        static readonly string[] ClipNames = { "Ellen_Idle", "EllenRandomIdle_1", "EllenRandomIdle_2", "EllenCombo_1", "EllenCombo_2", "EllenCombo_3", "EllenCombo_4", "Ellen_Run_Forward", "Ellen_Walk_Forward", "Ellen_JumpTakeOff", "Ellen_JumpPeak", "Ellen_Idle_Land_Fast", "Ellen_Death" };
        static readonly string[] Roles = { "BurnTarget", "ProtectedRobot", "FreezeTarget", "OverloadTarget", "HologramBody", "Caster", "Swordsman", "Traveler" };

        public static void PrepareAssets()
        {
            foreach (string sub in new[] { "Models", "Textures", "Animations", "Materials" }) Directory.CreateDirectory(CharacterRoot + "/" + sub);
            AssetDatabase.Refresh();
            Copy(Source + "/Ellen.fbx", CharacterRoot + "/Models/Ellen.fbx");
            var importer = (ModelImporter)AssetImporter.GetAtPath(CharacterRoot + "/Models/Ellen.fbx");
            if (importer.animationType != ModelImporterAnimationType.Human || !importer.isReadable || importer.importAnimation)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.isReadable = true; importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            foreach (string name in ClipNames)
            {
                string path = CharacterRoot + "/Animations/" + name + ".anim";
                Copy(Source + "/Animations/" + name + ".anim", path);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null || !clip.humanMotion) throw new InvalidOperationException("Expected humanoid clip: " + path);
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            }
            foreach (string name in new[] { "Albedo", "Normal", "MetallicSmooth", "Occlusion" })
            {
                string path = CharacterRoot + "/Textures/Ellen_Combined_" + name + ".tif";
                Copy(Source + "/Textures/Ellen/Ellen_Combined_" + name + ".tif", path);
                var textureImporter = (TextureImporter)AssetImporter.GetAtPath(path);
                textureImporter.textureType = name == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                textureImporter.sRGBTexture = name == "Albedo";
                textureImporter.maxTextureSize = 2048;
                textureImporter.SaveAndReimport();
            }
            string materialPath = CharacterRoot + "/Materials/Ellen_Base.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Ellen_Base" };
                material.SetTexture("_BaseMap", Texture("Albedo")); material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BumpMap", Texture("Normal")); material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", Texture("MetallicSmooth")); material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetTexture("_OcclusionMap", Texture("Occlusion")); material.EnableKeyword("_OCCLUSIONMAP");
                material.SetFloat("_Smoothness", .45f); material.SetFloat("_Metallic", 1);
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Copy(Source + "/Props/Ellen/Staff.fbx", CharacterRoot + "/Models/Staff.fbx");
            Copy(Source + "/Textures/Staff/Staff_Albedo.tif", CharacterRoot + "/Textures/Staff_Albedo.tif");
            string staffPath = CharacterRoot + "/Materials/Staff.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(staffPath) == null)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Staff" };
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterRoot + "/Textures/Staff_Albedo.tif"));
                m.SetFloat("_Smoothness", .4f); m.SetFloat("_Metallic", .3f); AssetDatabase.CreateAsset(m, staffPath);
            }
            string license = "F:/UnityProjects/OthersDemo/VisualEffectGraph-Samples-master/LICENSE.md";
            Copy(license, CharacterRoot + "/SOURCE_LICENSE.md");
            AssetDatabase.SaveAssets();
        }

        static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterRoot + "/Textures/Ellen_Combined_" + name + ".tif");
        static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(CharacterRoot + "/Animations/" + name + ".anim");
        static void Copy(string source, string target)
        {
            if (File.Exists(target)) return;
            File.Copy(source, target); AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
        }

        public static GameObject CreateCharacter(Transform parent, string role, Vector3 position, string sceneName)
        {
            var holder = new GameObject(role);
            holder.transform.SetParent(parent, false); holder.transform.localPosition = position;
            holder.transform.localRotation = Quaternion.Euler(0, role == "Caster" || role == "Swordsman" ? 90 : 145, 0);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRoot + "/Models/Ellen.fbx");
            if (model == null) throw new InvalidOperationException("Prepare Ellen assets first");
            var visual = Object.Instantiate(model, holder.transform); visual.name = "EllenVisual";
            var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) throw new InvalidOperationException("Ellen humanoid avatar is invalid");
            animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
            var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("No Ellen skinned mesh");
            Bounds bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            visual.transform.localScale *= 2.4f / Mathf.Max(.01f, bounds.size.y);
            bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            visual.transform.localPosition += Vector3.up * (holder.transform.position.y - bounds.min.y);
            string materialPath = Root + "/Materials/" + sceneName + "_Ellen_PLACEHOLDER.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(CharacterRoot + "/Materials/Ellen_Base.mat")) { name = sceneName + "_Ellen_PLACEHOLDER" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            foreach (var r in renderers)
            {
                r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                r.updateWhenOffscreen = true;
            }
            var motion = holder.AddComponent<FXCharacterMotion>(); motion.animator = animator; motion.idle = Clip("Ellen_Idle");
            motion.cues = CreateCues(sceneName);
            if (role == "Swordsman" || role == "Caster") AttachStaff(animator);
            return holder;
        }

        static void AttachStaff(Animator animator)
        {
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) throw new InvalidOperationException("Missing Ellen right hand");
            var socket = new GameObject("StaffSocket").transform; socket.SetParent(hand, false);
            socket.localRotation = Quaternion.Euler(0, 0, 90);
            var staff = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRoot + "/Models/Staff.fbx"), socket);
            staff.name = "EllenStaff";
            foreach (var renderer in staff.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CharacterRoot + "/Materials/Staff.mat");
        }

        static FXCharacterCue Cue(string clip, float start, float end, float sourceStart = 0, float sourceEnd = -1, float blend = .12f, bool loop = false)
            => new FXCharacterCue { clip = Clip(clip), start = start, end = end, sourceStart = sourceStart, sourceEnd = sourceEnd, blendIn = blend, blendOut = blend, loop = loop };

        static FXCharacterCue[] CreateCues(string scene)
        {
            switch (scene)
            {
                case "Overheat": return new[] { Cue("Ellen_Idle_Land_Fast", .08f, 1, .45f, 1.15f), Cue("Ellen_Death", 3.7f, 4.45f, 0, .16f), Cue("Ellen_Idle_Land_Fast", 4.45f, 5.6f, .45f, 1.15f) };
                case "ReactiveShield": return new[] { Cue("Ellen_Idle_Land_Fast", 1.42f, 2.25f, .45f, 1.15f), Cue("Ellen_Idle_Land_Fast", 3.52f, 4.35f, .45f, 1.15f), Cue("Ellen_Idle_Land_Fast", 5.62f, 6.45f, .45f, 1.15f) };
                case "CryoFreeze": return new[] { Cue("Ellen_Death", .6f, 3, 0, .16f), Cue("Ellen_Death", 2.85f, 4.65f, .16f, .16f), Cue("Ellen_Idle_Land_Fast", 4.5f, 5.8f, .45f, 1.15f) };
                case "ElectricOverload": return new[] { Cue("Ellen_Death", .8f, 4.25f, 0, .16f), Cue("Ellen_Idle_Land_Fast", 4.1f, 5.5f, .45f, 1.15f) };
                case "HologramMaterialize": return new[] { Cue("EllenRandomIdle_1", 2.4f, 4.4f, .3f, 2.3f) };
                case "ArcaneChargeShot": return new[] { Cue("EllenCombo_3", .35f, 1.65f, 0, .28f), Cue("EllenCombo_3", 1.52f, 2.35f, .28f, .9f, .08f), Cue("Ellen_Idle_Land_Fast", 2.25f, 3.3f, .3f, 1.15f) };
                case "GroundSlash": return new[] { Cue("EllenCombo_1", .25f, 1.2f, 0, .6f, .06f), Cue("Ellen_Idle_Land_Fast", 1.05f, 2.1f, .2f, 1.15f) };
                case "FoldstepPortal": return new[] { Cue("Ellen_Run_Forward", 1.1f, 2.15f, 0, -1, .1f, true), Cue("Ellen_JumpTakeOff", 1.85f, 2.3f, 0, .3f), Cue("Ellen_Idle_Land_Fast", 2.55f, 3.7f) };
                default: return Array.Empty<FXCharacterCue>();
            }
        }

        public static int UpgradeEffect(GameObject effect, string sceneName)
        {
            var driver = effect.GetComponent<FXSequenceDriver>();
            if (driver == null) return 0;
            int changed = 0;
            foreach (string role in Roles)
            {
                Transform old = effect.transform.Find(role);
                if (old == null || old.GetComponent<FXCharacterMotion>() != null) continue;
                Renderer[] oldRenderers = old.GetComponentsInChildren<Renderer>();
                var bindings = driver.materials.Where(b => oldRenderers.Contains(b.target)).ToArray();
                var newCharacter = CreateCharacter(effect.transform, role, old.localPosition, sceneName);
                var newRenderers = newCharacter.GetComponentsInChildren<SkinnedMeshRenderer>();
                var template = bindings.FirstOrDefault();
                var retained = driver.materials.Where(b => !oldRenderers.Contains(b.target)).ToList();
                foreach (var r in newRenderers)
                {
                    if (template != null && !template.previewPlaceholder && template.target != null)
                        r.sharedMaterials = Enumerable.Repeat(template.target.sharedMaterial, r.sharedMaterials.Length).ToArray();
                    retained.Add(new FXMaterialBinding { target = r, previewMode = template != null ? template.previewMode : FXPreviewMode.Surface,
                        previewColor = template != null ? template.previewColor : Color.white, previewPlaceholder = template == null || template.previewPlaceholder, preserveAlbedo = true });
                }
                driver.materials = retained.ToArray();
                foreach (var cue in driver.particles)
                {
                    if (cue.system == null) continue;
                    var shape = cue.system.shape;
                    if (shape.skinnedMeshRenderer != null && oldRenderers.Contains(shape.skinnedMeshRenderer))
                        shape.skinnedMeshRenderer = newRenderers.OrderByDescending(r => r.sharedMesh.vertexCount).First();
                }
                Object.DestroyImmediate(old.gameObject); changed++;
            }
            driver.characters = effect.GetComponentsInChildren<FXCharacterMotion>(true);
            return changed;
        }

        public static void UpgradeAndVerify()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string backup = "RepairBackups/EllenUpgrade_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup);
            PrepareAssets();
            var report = new StringBuilder("# Ellen resource and migration manifest\n\nSource: `" + Source + "`. Original projects are read-only. New local GUIDs; no sample scenes, VFX graphs or HDRP shaders copied.\n\n");
            report.AppendLine("Imported Ellen.fbx, Staff.fbx, four Ellen PBR textures, Staff albedo and 13 humanoid animation clips. Source license: Characters/Ellen/SOURCE_LICENSE.md.\n");
            foreach (string path in Directory.GetFiles(Root + "/Scenes", "*.unity"))
            {
                File.Copy(path, backup + "/" + Path.GetFileName(path));
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var driver = Object.FindFirstObjectByType<FXSequenceDriver>();
                string name = Path.GetFileNameWithoutExtension(path);
                int count = UpgradeEffect(driver.gameObject, name);
                var environment = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Environment");
                Transform floor = environment != null ? environment.transform.Find("Floor") : null;
                if (floor != null) Object.DestroyImmediate(floor.gameObject);
                driver.ResetSequence();
                foreach (var motion in driver.characters) motion.ReleaseGraph();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                report.AppendLine("- " + name + ": " + count + " character replacement(s); Environment/Floor removed; disc retained.");
            }
            foreach (string path in Directory.GetFiles(Root + "/Prefabs", "*_FX.prefab"))
            {
                File.Copy(path, backup + "/" + Path.GetFileName(path));
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    UpgradeEffect(contents, Path.GetFileNameWithoutExtension(path).Replace("_FX", ""));
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            report.AppendLine("\nBackup: `" + backup + "`. Scene clips and effect object names retained. Existing custom character materials preserved where Preview Placeholder was disabled; remap their base textures to Ellen manually.\n");
            foreach (string name in ClipNames) report.AppendLine("- " + name + ": " + Clip(name).length.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "s");
            File.WriteAllText(Root + "/Documentation/EllenResourceManifest.md", report.ToString(), new UTF8Encoding(false));
            AssetDatabase.SaveAssets();
            PortfolioSceneBuilder.ValidateAndCapture();
            PortfolioPlayModeCheck.RunBatch();
        }

        public static void RefineAndVerify()
        {
            foreach (string path in Directory.GetFiles(Root + "/Scenes", "*.unity"))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var driver = Object.FindFirstObjectByType<FXSequenceDriver>();
                foreach (var motion in driver.characters) { motion.ReleaseGraph(); motion.cues = CreateCues(scene.name); }
                driver.ResetSequence();
                foreach (var motion in driver.characters) motion.ReleaseGraph();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            foreach (string path in Directory.GetFiles(Root + "/Prefabs", "*_FX.prefab"))
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var motion in contents.GetComponentsInChildren<FXCharacterMotion>())
                        motion.cues = CreateCues(Path.GetFileNameWithoutExtension(path).Replace("_FX", ""));
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            AssetDatabase.SaveAssets();
            Audit();
            PortfolioSceneBuilder.ValidateAndCapture();
            PortfolioPlayModeCheck.RunBatch();
        }

        public static void Audit()
        {
            var report = new StringBuilder("# Ellen animation and stage audit\n\n");
            foreach (string path in Directory.GetFiles(Root + "/Scenes", "*.unity"))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (scene.GetRootGameObjects().Any(g => g.name == "Environment" && g.transform.Find("Floor") != null)) throw new InvalidOperationException("Outer floor remains: " + scene.name);
                var driver = Object.FindFirstObjectByType<FXSequenceDriver>();
                foreach (var motion in driver.characters)
                {
                    if (motion == null || motion.animator == null || !motion.animator.avatar.isHuman) throw new InvalidOperationException("Broken Ellen binding: " + scene.name);
                    foreach (var cue in motion.cues)
                        if (cue.clip == null || cue.start < 0 || cue.end > driver.duration || cue.end <= cue.start || cue.sourceStart > cue.clip.length || cue.sourceEnd > cue.clip.length + .001f)
                            throw new InvalidOperationException("Invalid character cue: " + scene.name);
                    var hand = motion.animator.GetBoneTransform(HumanBodyBones.RightHand);
                    driver.Seek(0); Vector3 first = hand.position;
                    float t = motion.cues.Length > 0 ? (motion.cues[0].start + motion.cues[0].end) * .5f : 1;
                    driver.Seek(t); Vector3 expected = hand.position;
                    if (Vector3.Distance(first, expected) < .005f) throw new InvalidOperationException("No observable bone animation: " + scene.name);
                    driver.Seek(driver.duration * .9f); driver.Seek(t);
                    if (Vector3.Distance(expected, hand.position) > .002f) throw new InvalidOperationException("Non-deterministic pose seek: " + scene.name);
                    var skinned = motion.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var r in skinned)
                        if (!AssetDatabase.GetAssetPath(r.sharedMesh).Contains("Characters/Ellen")) throw new InvalidOperationException("Old character mesh remains");
                    foreach (var cue in driver.particles)
                        if (cue.system.shape.shapeType == ParticleSystemShapeType.SkinnedMeshRenderer && !skinned.Contains(cue.system.shape.skinnedMeshRenderer)) throw new InvalidOperationException("Old particle mesh reference");
                    report.AppendLine("- PASS " + scene.name + ": Ellen avatar/mesh, " + motion.cues.Length + " valid cues, animated hand displacement=" + Vector3.Distance(first, expected).ToString("F3") + "m, repeatable seek, surface-particle bindings.");
                    driver.ResetSequence(); motion.ReleaseGraph();
                }
                report.AppendLine("- PASS " + scene.name + ": no Environment/Floor.");
            }
            foreach (string path in Directory.GetFiles(Root + "/Prefabs", "*_FX.prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (AssetDatabase.GetAssetPath(skin.sharedMesh).Contains("SpaceRobotKyle")) throw new InvalidOperationException("Kyle remains in prefab: " + path);
            }
            File.WriteAllText(Root + "/Verification/EllenAudit.md", report.ToString(), new UTF8Encoding(false));
            Debug.Log("PORTFOLIO_ELLEN_AUDIT_SUCCESS");
        }
    }
}

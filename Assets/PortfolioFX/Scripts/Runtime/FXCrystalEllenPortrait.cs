using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PortfolioFX
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FXCrystalEllenPortrait : MonoBehaviour
    {
        public FXSequenceDriver sequence;
        public Renderer crystal;
        public GameObject ellenPrefab;
        public Material ellenMaterial;
        public AnimationClip idle;
        public AnimationClip action;
        [Range(128, 1024)] public int resolution = 512;
        GameObject rig;
        Camera capture;
        FXCharacterMotion motion;
        RenderTexture texture;
        MaterialPropertyBlock block;
        float lastTime = float.NaN;
        static readonly int InsideTex = Shader.PropertyToID("_InsideTex");

        void OnEnable() { lastTime = float.NaN; }

        void LateUpdate()
        {
            if (sequence == null || crystal == null || ellenPrefab == null || ellenMaterial == null || idle == null) return;
            if (rig == null && !Build()) return;
            float time = sequence.CurrentTime;
            if (!Application.isPlaying && Mathf.Approximately(time, lastTime) && texture.IsCreated()) return;
            lastTime = time;
            motion.Sample(time);
            if (!texture.IsCreated()) texture.Create();
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(capture, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
            else capture.Render();
            block ??= new MaterialPropertyBlock();
            crystal.GetPropertyBlock(block);
            block.SetTexture(InsideTex, texture);
            crystal.SetPropertyBlock(block);
        }

        bool Build()
        {
            // Keep the capture outside the stage and isolate its lights with layer 31.
            rig = new GameObject("EllenPortraitCapture") { hideFlags = HideFlags.HideAndDontSave };
            rig.transform.position = new Vector3(1000, 1000, 1000);
            var model = Instantiate(ellenPrefab, rig.transform);
            model.name = "Ellen";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0, 180, 0);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 31;
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0 || model.GetComponent<Animator>() == null)
            {
                Debug.LogError("Ellen portrait requires a skinned model with a root Animator.", this);
                OnDisable();
                enabled = false;
                return false;
            }
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = ellenMaterial;
                r.sharedMaterials = materials;
                r.updateWhenOffscreen = true;
                bounds.Encapsulate(r.bounds);
            }
            model.transform.localScale *= 2.4f / Mathf.Max(.01f, bounds.size.y);
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            model.transform.position += rig.transform.position + Vector3.up * 1.2f - bounds.center;
            motion = model.AddComponent<FXCharacterMotion>();
            motion.animator = model.GetComponent<Animator>();
            motion.idle = idle;
            motion.cues = new[] { new FXCharacterCue { clip = action, start = .5f, end = 3.8f, blendIn = .3f, blendOut = .3f } };
            var cameraObject = new GameObject("PortraitCamera") { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.2f, -5);
            capture = cameraObject.AddComponent<Camera>();
            capture.enabled = false;
            capture.orthographic = true;
            capture.orthographicSize = 1.55f;
            capture.nearClipPlane = .1f;
            capture.farClipPlane = 12;
            capture.cullingMask = 1 << 31;
            capture.clearFlags = CameraClearFlags.SolidColor;
            capture.backgroundColor = Color.clear;
            capture.allowHDR = true;
            var cameraData = capture.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.requiresColorTexture = false;
            cameraData.requiresDepthTexture = false;
            cameraData.volumeLayerMask = 0;
            texture = new RenderTexture(resolution, resolution * 2, 24, RenderTextureFormat.ARGBHalf)
            {
                name = "EllenCrystalPortrait", hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            texture.Create();
            capture.targetTexture = texture;
            CreateLight(new Vector3(-2, 3, -2), new Color(1, .92f, .85f), 8);
            CreateLight(new Vector3(2, 2, -1), new Color(.65f, .82f, 1), 5);
            return true;
        }

        void CreateLight(Vector3 position, Color color, float intensity)
        {
            var go = new GameObject("PortraitLight") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(rig.transform, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7;
            light.intensity = intensity;
            light.color = color;
            light.cullingMask = 1 << 31;
            light.shadows = LightShadows.None;
        }

        void OnDisable()
        {
            if (motion != null) motion.ReleaseGraph();
            if (crystal != null)
            {
                block ??= new MaterialPropertyBlock();
                crystal.GetPropertyBlock(block);
                block.SetTexture(InsideTex, Texture2D.blackTexture);
                crystal.SetPropertyBlock(block);
            }
            if (texture != null) texture.Release();
            if (Application.isPlaying) { Destroy(rig); Destroy(texture); }
            else { DestroyImmediate(rig); DestroyImmediate(texture); }
            rig = null; texture = null; capture = null; motion = null;
        }
    }
}

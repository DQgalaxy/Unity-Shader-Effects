using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PortfolioFX
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FXOverheatFlames : MonoBehaviour
    {
        [Serializable]
        public sealed class Attachment
        {
            public Renderer flame;
            public Transform anchor;
            public Transform end;
            [Range(0, 1)] public float alongBone;
            public float heightOffset;
            public float surfaceOffset = .07f;
            public float flowPhase;
        }

        public FXSequenceDriver sequence;
        public Camera presentationCamera;
        public Attachment[] attachments = Array.Empty<Attachment>();
        MaterialPropertyBlock block;
        static readonly int EffectTime = Shader.PropertyToID("_EffectTime");

        void OnEnable() { RenderPipelineManager.beginCameraRendering += BeforeCamera; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= BeforeCamera; }
        void LateUpdate() { Refresh(presentationCamera != null ? presentationCamera : Camera.main); }

        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType == CameraType.Game || camera.cameraType == CameraType.SceneView)
                Refresh(camera);
        }

        public void Refresh(Camera camera)
        {
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var item in attachments)
            {
                if (item == null || item.flame == null || item.anchor == null) continue;
                Vector3 anchor = item.end != null
                    ? Vector3.Lerp(item.anchor.position, item.end.position, item.alongBone)
                    : item.anchor.position;
                Vector3 towardCamera = camera != null ? camera.transform.position - anchor : Vector3.back;
                towardCamera.y = 0;
                if (towardCamera.sqrMagnitude < .0001f) towardCamera = Vector3.back;
                towardCamera.Normalize();
                Transform card = item.flame.transform;
                card.rotation = Quaternion.LookRotation(-towardCamera, Vector3.up);
                // The clip still owns scale. Pin the flame base to a bone, not its centre.
                float height = Mathf.Abs(card.lossyScale.y);
                card.position = anchor + Vector3.up * (item.heightOffset + height * .42f)
                    + towardCamera * item.surfaceOffset;
                item.flame.shadowCastingMode = ShadowCastingMode.Off;
                item.flame.receiveShadows = false;
                item.flame.GetPropertyBlock(block);
                block.SetFloat(EffectTime, (sequence != null ? sequence.CurrentTime : 0) + item.flowPhase);
                item.flame.SetPropertyBlock(block);
                block.Clear();
            }
        }
    }
}

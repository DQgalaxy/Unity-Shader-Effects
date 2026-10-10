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
    public static class FireballPresentationVerification
    {
        [MenuItem("Portfolio FX/Verify Fireball Presentation")]
        public static void Run()
        {
            const string output="Logs/FireballUpgrade";
            Directory.CreateDirectory(output);Scene scene=default;bool passed=false;
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                scene=EditorSceneManager.OpenPreviewScene("Assets/PortfolioFX/Scenes/ArcaneChargeShot.unity");
                var fx=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FXFireballPresentation>()).Single();
                fx.presentationCamera.scene=scene;
                fx.sequence.Pause();fx.sequence.Seek(0);fx.Refresh();
                if(fx.FragmentCount<6)throw new Exception("Insufficient source-mesh fragments.");
                if(!fx.targetMesh.isReadable)throw new Exception("Target mesh not readable.");
                foreach(float t in new[]{0f,.8f,1.6f,1.8f,2.15f,2.5f,2.85f,4.2f,6.4f})
                {
                    fx.sequence.Seek(t);fx.Refresh();
                    if(fx.weapon.activeInHierarchy)throw new Exception("Weapon reappeared.");
                    if(fx.legacyRenderers.Any(r=>r!=null&&r.enabled))throw new Exception("Placeholder renderer reappeared.");
                    if(fx.TargetRenderer.enabled!=(t<fx.hitTime+.035f))throw new Exception("Target break timing mismatch.");
                    if(t>=.8f&&t<=2.15f&&!fx.FireRenderer.enabled)throw new Exception("Missing fireball during charge/flight.");
                    if(t>=4.2f&&fx.FireRenderer.enabled)throw new Exception("Fireball survived impact.");
                    foreach(var r in fx.GetComponentsInChildren<Renderer>())foreach(var mat in r.sharedMaterials.Where(m=>m!=null))
                        foreach(var msg in ShaderUtil.GetShaderMessages(mat.shader))if(msg.severity.ToString()=="Error")throw new Exception(msg.message);
                    Capture(fx.presentationCamera,$"{output}/Fireball_{t:0.00}.png");
                }
                fx.sequence.Seek(2.1f);fx.Refresh();Vector3 first=fx.FireRenderer.transform.position;
                fx.sequence.Seek(4);fx.Refresh();fx.sequence.Seek(2.1f);fx.Refresh();
                if(Vector3.Distance(first,fx.FireRenderer.transform.position)>.0001f)throw new Exception("Projectile seek is nondeterministic.");
                fx.sequence.Seek(0);fx.Refresh();
                if(!fx.TargetRenderer.enabled||fx.FireRenderer.enabled)throw new Exception("Loop reset failed.");
                if(Vector3.Distance(fx.Flight(fx.hitTime),fx.HitPosition)>.0001f)throw new Exception("Projectile missed impact point.");
                File.WriteAllText(output+"/unity-result.txt","PASS: imported meshes and textures, shader compilation, hand cast, trail, target swap, deterministic seek/reset; screenshots require visual inspection.");
                passed=true;
            }
            catch(Exception e){File.WriteAllText(output+"/unity-result.txt","FAIL: "+e);Debug.LogException(e);}
            finally
            {
                if(scene.IsValid())
                {
                    foreach(var m in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FXCharacterMotion>()))m.ReleaseGraph();
                    EditorSceneManager.ClosePreviewScene(scene);
                }
                if(Application.isBatchMode)EditorApplication.Exit(passed?0:1);
            }
        }
        static void Capture(Camera camera,string file)
        {
            var rt=new RenderTexture(1280,800,24,RenderTextureFormat.ARGBHalf);
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try
            {
                rt.Create();camera.targetTexture=rt;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
                var pixels=image.GetPixels32();int nonblank=0,magenta=0;
                for(int i=0;i<pixels.Length;i+=37){if(!pixels[i].Equals(pixels[0]))nonblank++;if(pixels[i].r>230&&pixels[i].b>230&&pixels[i].g<30)magenta++;}
                if(nonblank<100||magenta>100)throw new Exception("Blank capture or error shader.");
                File.WriteAllBytes(file,image.EncodeToPNG());
            }
            finally{camera.targetTexture=previous;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}

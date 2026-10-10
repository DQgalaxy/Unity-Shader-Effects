using System;
using System.Collections.Generic;
using System.Globalization;
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
    public static class RemainingFXVerification
    {
        const string Output="Logs/RemainingFX";
        [MenuItem("Portfolio FX/Verify Chapters 17-24")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var report=new List<string>();bool passed=false;
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Check("MeteorStrike",new[]{0f,1f,1.95f,2.35f,2.48f,3.2f,6.8f,1.95f},report);
                Check("FoldstepPortal",new[]{0f,1.3f,1.75f,2.3f,3f,3.7f,5.6f,5.9f,0f,1.3f},report);
                Check("WaterShockwave",new[]{.8f,1.42f,1.8f,2.8f,4.8f,1.8f},report);
                Check("DepthIntersectionLab",new[]{.6f,1.5f,3f,5f,1.5f},report);
                passed=true;report.Add("PASS: bindings, shader compilation, preview states, loop rewind and nonblank captures. Inspect PNGs for composition.");
            }
            catch(Exception e){report.Add("FAIL: "+e);Debug.LogException(e);}
            finally
            {
                File.WriteAllLines(Output+"/unity-verification.txt",report);
                if(Application.isBatchMode)EditorApplication.Exit(passed?0:1);
            }
        }
        static void Check(string name,float[] times,List<string> report)
        {
            Scene scene=EditorSceneManager.OpenPreviewScene("Assets/PortfolioFX/Scenes/"+name+".unity");
            try
            {
                var roots=scene.GetRootGameObjects();
                var driver=roots.SelectMany(r=>r.GetComponentsInChildren<FXSequenceDriver>()).Single();
                var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>()).Single();
                camera.scene=scene;driver.Pause();
                var data=camera.GetUniversalAdditionalCameraData();
                Require(data.requiresColorTexture&&data.requiresDepthTexture,"Camera depth/color textures: "+name);
                var meteor=driver.GetComponent<FXMeteorPresentation>();var portal=driver.GetComponent<FXPortalPresentation>();
                var water=driver.GetComponent<FXWaterPresentation>();var depth=driver.GetComponent<FXDepthLabPresentation>();
                Require((meteor&&meteor.Ready)||(portal&&portal.Ready)||(water&&water.Ready)||(depth&&depth.Ready),"Missing presentation binding: "+name);
                Require(driver.materials.All(m=>!m.previewPlaceholder),"Placeholder driver: "+name);
                foreach(float time in times)
                {
                    driver.Seek(time);
                    foreach(var component in driver.GetComponents<MonoBehaviour>())Editor.FXRemainingPresentationEditor.Refresh(component);
                    if(meteor){
                        Require(meteor.body.enabled==(time>=1.6f&&time<2.49f),"Meteor visibility");
                        if(Mathf.Approximately(time,2.48f))Require(meteor.LiveParticles>0,"Meteor impact particles");
                        if(time>6.5f)Require(meteor.LiveParticles==0,"Meteor cleanup");
                    }
                    if(portal){
                        if(Mathf.Approximately(time,2.3f))Require(!portal.traveler.enabled,"Traveler visible between gates");
                        if(Mathf.Approximately(time,3.7f))Require(portal.traveler.enabled,"Traveler did not reappear");
                        if(time>5.5f)Require(portal.surfaces.All(r=>!r.enabled)&&portal.frames.All(r=>!r.enabled),"Portal closure");
                        if(time>5.75f||time==0)Require(!portal.traveler.enabled,"Traveler visible during loop return");
                    }
                    if(water){
                        Require(water.water.GetComponent<MeshFilter>().sharedMesh.vertexCount==4225,"Water grid subdivision lost");
                        Require(water.drop.enabled==(time<water.impactTime),"Water impact timing");
                    }
                    foreach(var r in roots.SelectMany(x=>x.GetComponentsInChildren<Renderer>())){
                        foreach(var mat in r.sharedMaterials)Require(mat&&mat.shader,"Missing material or shader on "+r.name);
                        var b=r.bounds;Require(float.IsFinite(b.center.x)&&float.IsFinite(b.extents.y),"Nonfinite renderer bounds");
                    }
                    string label=name+"_"+time.ToString("0.00",CultureInfo.InvariantCulture);
                    Capture(camera,Output+"/"+label+".png");
                    foreach(var shader in roots.SelectMany(x=>x.GetComponentsInChildren<Renderer>()).SelectMany(r=>r.sharedMaterials).Select(m=>m.shader).Distinct())
                        foreach(var message in ShaderUtil.GetShaderMessages(shader))
                            if(message.severity.ToString()=="Error")throw new Exception(shader.name+": "+message.message);
                    if(depth&&Mathf.Approximately(time,1.5f)){
                        depth.useSoftFade=false;depth.Refresh();Capture(camera,Output+"/Depth_Hard.png");
                        depth.useSoftFade=true;depth.Refresh();Capture(camera,Output+"/Depth_Soft.png");
                    }
                    report.Add(label+": captured");
                }
            }
            finally
            {
                foreach(var m in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FXCharacterMotion>()))m.ReleaseGraph();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,800,24,RenderTextureFormat.ARGBHalf);
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            try
            {
                rt.Create();camera.targetTexture=rt;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
                var pixels=image.GetPixels32();var first=pixels[0];int varied=0,magenta=0;
                foreach(var p in pixels){if(Math.Abs(p.r-first.r)+Math.Abs(p.g-first.g)+Math.Abs(p.b-first.b)>25)varied++;
                    if(p.r>220&&p.b>220&&p.g<50)magenta++;}
                File.WriteAllBytes(path,image.EncodeToPNG());
                Require(varied>pixels.Length*.025f,"Blank render: "+path);
                Require(magenta<pixels.Length*.01f,"Possible error shader pixels: "+path);
            }
            finally
            {
                camera.targetTexture=oldTarget;RenderTexture.active=oldActive;rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}

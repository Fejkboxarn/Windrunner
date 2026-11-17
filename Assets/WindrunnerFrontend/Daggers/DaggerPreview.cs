using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Windrunner.Daggers
{
    // A posed mesh snapshot. No cloned animator, camera target, gameplay object or listener.
    public sealed class DaggerPreview : IDisposable
    {
        readonly GameObject stage,subject;
        readonly Camera camera;
        readonly RenderTexture texture;
        readonly List<Mesh> baked=new();
        readonly Transform left,right;
        GameObject leftModel,rightModel;
        const int Layer=31;
        public DaggerPreview(DaggerPlayer player,RawImage surface)
        {
            stage=new GameObject("Arsenal preview studio");stage.SetActive(false);stage.transform.position=new Vector3(0,6000,0);
            subject=new GameObject("Character study");subject.transform.SetParent(stage.transform,false);
            Transform CopyPose(Transform source,string name)
            {
                var dest=new GameObject(name).transform;dest.SetParent(subject.transform,false);
                var basis=player.transform;
                dest.localPosition=Quaternion.Inverse(basis.rotation)*(source.position-basis.position);
                dest.localRotation=Quaternion.Inverse(basis.rotation)*source.rotation;dest.localScale=source.lossyScale;
                dest.gameObject.layer=Layer;return dest;
            }
            left=CopyPose(player.LeftGrip,"Left grip");right=CopyPose(player.RightGrip,"Right grip");
            Bounds bounds=default;bool first=true;
            foreach(var renderer in player.rig.GetComponentsInChildren<Renderer>(false))
            {
                if(!renderer.enabled || renderer.forceRenderingOff || renderer is ParticleSystemRenderer ||
                    (player.LeftModel && renderer.transform.IsChildOf(player.LeftModel.transform)) ||
                    (player.RightModel && renderer.transform.IsChildOf(player.RightModel.transform)))continue;
                Mesh mesh;
                if(renderer is SkinnedMeshRenderer skin){mesh=new Mesh();skin.BakeMesh(mesh);baked.Add(mesh);}
                else if(renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))mesh=filter.sharedMesh;
                else continue;
                if(!mesh)continue;
                var part=CopyPose(renderer.transform,renderer.name);
                part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                var world=renderer.bounds;
                if(first){bounds=world;first=false;}else bounds.Encapsulate(world);
            }
            var cameraObject=new GameObject("Portrait camera");cameraObject.transform.SetParent(stage.transform,false);
            camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<Layer;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.108f,.119f);
            var center=Quaternion.Inverse(player.transform.rotation)*(bounds.center-player.transform.position);
            var height=Mathf.Max(1.5f,bounds.size.y);
            camera.transform.localPosition=center+new Vector3(0,height*.035f,height*player.catalog.previewDistance);
            camera.transform.LookAt(stage.transform.TransformPoint(center));camera.fieldOfView=37;camera.nearClipPlane=.03f;camera.farClipPlane=height*5;
            var lamp=new GameObject("Portrait key");lamp.transform.SetParent(stage.transform,false);
            var light=lamp.AddComponent<Light>();light.type=LightType.Directional;light.intensity=player.catalog.previewKeyIntensity;light.cullingMask=1<<Layer;
            lamp.transform.localRotation=Quaternion.Euler(38,147,0);
            texture=new RenderTexture(600,800,24){name="Arsenal portrait"};
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
            {texture.Create();camera.targetTexture=texture;surface.texture=texture;}
        }
        public void Select(DaggerPair pair)
        {
            void Replace(ref GameObject old,Transform socket,GameObject prefab,Vector3 offset,Vector3 angles)
            {
                if(old){old.SetActive(false);UnityEngine.Object.Destroy(old);}
                old=null;if(!prefab)return;
                old=UnityEngine.Object.Instantiate(prefab,socket,false);
                old.transform.localPosition=offset;old.transform.localRotation=Quaternion.Euler(angles);
                foreach(var t in old.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
            }
            Replace(ref leftModel,left,pair?pair.leftModel:null,pair?pair.leftOffset:default,pair?pair.leftEuler:default);
            Replace(ref rightModel,right,pair?pair.rightModel:null,pair?pair.rightOffset:default,pair?pair.rightEuler:default);
            Render();
        }
        public void Rotate(float degrees){subject.transform.Rotate(Vector3.up,degrees,Space.Self);Render();}
        void Render()
        {
            if(!texture.IsCreated())return;
            stage.SetActive(true);
            try{camera.Render();}finally{stage.SetActive(false);}
        }
        public void Dispose()
        {
            if(camera)camera.targetTexture=null;
            texture.Release();UnityEngine.Object.Destroy(texture);
            foreach(var mesh in baked)UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(stage);
        }
    }
}

#if UNITY_EDITOR || WINDRUNNER_MOVEMENT_LAB
using UnityEngine;
using UnityEngine.InputSystem;

namespace Windrunner.MovementLab
{
    public sealed class LabFlightRig : MonoBehaviour
    {
        private MovementLabPlayer player;
        private GameObject previousCamera;
        private InputAction toggle, reset, move, look, up, down, sprint;
        private InputAction[] stations;
        private float yaw, pitch;
        private Vector3 velocity;
        private int entry;
        internal void Enter(MovementLabPlayer owner,GameObject source,InputAction toggleAction,InputAction[] stationActions,InputAction resetAction)
        {
            player=owner;previousCamera=source;toggle=toggleAction;stations=stationActions;reset=resetAction;
            var map=toggle.actionMap;
            move=map.FindAction("Move",true);look=map.FindAction("Look",true);up=map.FindAction("SwimUp",true);
            down=map.FindAction("SwimDown",true);sprint=map.FindAction("Dash",true);
            transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
            yaw=transform.eulerAngles.y;pitch=Mathf.DeltaAngle(0,transform.eulerAngles.x);
            gameObject.AddComponent<Camera>().CopyFrom(source.GetComponent<Camera>());
            source.SetActive(false);
            gameObject.AddComponent<AudioListener>();
            entry=Time.frameCount;owner.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if(player==null || Time.frameCount==entry || player.inputHost.Input.IsGameplayBlocked || Time.deltaTime<=0) return;
            for(int i=0;i<stations.Length;i++) if(stations[i].WasPressedThisFrame()){Exit(i,true);return;}
            if(toggle.WasPressedThisFrame() || reset.WasPressedThisFrame()){Exit(player.destinationIndex,reset.WasPressedThisFrame());return;}
            var delta=look.ReadValue<Vector2>();
            yaw+=delta.x*.12f;pitch=Mathf.Clamp(pitch-delta.y*.12f,-85,85);
            transform.rotation=Quaternion.Euler(pitch,yaw,0);
            Vector2 axes=move.ReadValue<Vector2>();
            Vector3 direction=transform.forward*axes.y+transform.right*axes.x+Vector3.up*((up.IsPressed()?1:0)-(down.IsPressed()?1:0));
            float speed=player.profile.flightSpeed*(sprint.IsPressed()?player.profile.flightSprint:1);
            velocity=Vector3.MoveTowards(velocity,Vector3.ClampMagnitude(direction,1)*speed,speed*6*Time.deltaTime);
            transform.position+=velocity*Time.deltaTime;
        }
        private void Exit(int station,bool refill)
        {
            gameObject.SetActive(false);previousCamera.SetActive(true);player.gameObject.SetActive(true);
            if(refill) player.Teleport(station);
            else player.PlaceAtStart(station,false);
            Destroy(gameObject);
        }
        private void OnGUI()
        {
            GUI.Box(new Rect(12,12,540,56),"INSPECTION FLIGHT    "+move.GetBindingDisplayString()+" move   "+sprint.GetBindingDisplayString()+" fast\n"+
                up.GetBindingDisplayString()+" up   "+down.GetBindingDisplayString()+" down    "+toggle.GetBindingDisplayString()+" return to station pad");
        }
    }
}
#endif

using UnityEngine;
using UnityEngine.InputSystem;
namespace Windrunner.Suntemple
{
    // Only the isolated imported demo controllers use this adapter. Production input is untouched.
    public static class SuntempleInput
    {
        static Key KeyFor(string name)=>name=="space"?Key.Space:(Key)System.Enum.Parse(typeof(Key),name,true);
        public static bool GetKey(string name)=>Keyboard.current!=null&&Keyboard.current[KeyFor(name)].isPressed;
        public static bool GetKeyDown(string name)=>Keyboard.current!=null&&Keyboard.current[KeyFor(name)].wasPressedThisFrame;
        public static bool GetKey(KeyCode code)=>Keyboard.current!=null&&Keyboard.current[(Key)System.Enum.Parse(typeof(Key),code==KeyCode.LeftControl?"LeftCtrl":code.ToString())].isPressed;
        public static float GetAxis(string axis)
        {
            if(axis=="Mouse X")return Mouse.current?.delta.ReadValue().x*.05f??0;
            if(axis=="Mouse Y")return Mouse.current?.delta.ReadValue().y*.05f??0;
            if(Keyboard.current==null)return 0;
            if(axis=="Horizontal")return (GetKey("d")?1:0)-(GetKey("a")?1:0);
            if(axis=="Vertical")return (GetKey("w")?1:0)-(GetKey("s")?1:0);
            return 0;
        }
    }
}

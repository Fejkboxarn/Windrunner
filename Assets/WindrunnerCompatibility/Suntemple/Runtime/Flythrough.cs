// Project-owned adaptation of the locally imported Polyart demo controller. Original retained.
﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Windrunner.Suntemple
{
    public class Flythrough : MonoBehaviour
    {
        //float lookSpeed = 1.0f;
        float moveSpeed = 0.07f;

        public GameObject sun;
        public GameObject rimLight;
        public Camera mycamera;
        private Quaternion baseSunTransform;
        private Quaternion baseRimLightTransform;
        public float sunX;
        public float sunY;
        public float sunZ;
        public float defaultFov;
        public float baseSpeed = 0.07f;
        public float runSpeed = 0.3f;
        public float dashSpeed = 2f;
        // Start is called before the first frame update
        void Start()
        {
            mycamera.fieldOfView = defaultFov;
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = false;
            if (sun) baseSunTransform = sun.transform.rotation;
            if (rimLight) baseRimLightTransform = rimLight.transform.rotation;
        }


        // Update is called once per frame
        void Update()
        {

            if (SuntempleInput.GetKey("e"))
            {
                if (sun) sun.transform.Rotate(0, 0.1f, 0, Space.World);
                if (rimLight) rimLight.transform.Rotate(0, 0.1f, 0, Space.World);
            }
            if (SuntempleInput.GetKey("q"))
            {
                if (sun) sun.transform.Rotate(0, -0.1f, 0, Space.World);
                if (rimLight) rimLight.transform.Rotate(0, -0.1f, 0, Space.World);
            }
            if (SuntempleInput.GetKey("z"))
            {
                if (sun) sun.transform.Rotate(-0.1f, 0, 0, Space.World);
                if (rimLight) rimLight.transform.Rotate(-0.1f, 0, 0, Space.World);
            }
            if (SuntempleInput.GetKey("x"))
            {
                if (sun) sun.transform.Rotate(0.1f, 0, 0, Space.World);
                if (rimLight) rimLight.transform.Rotate(0.1f, 0, 0, Space.World);
            }
            if (SuntempleInput.GetKeyDown("t"))
            {
                if (sun) sun.transform.rotation = baseSunTransform;
                if (rimLight) rimLight.transform.rotation = baseRimLightTransform;
            }
            if (SuntempleInput.GetKey("v"))
            {
                mycamera.fieldOfView -= 0.3f;
            }
            if (SuntempleInput.GetKey("b"))
            {
                mycamera.fieldOfView += 0.3f;
            }
            if (SuntempleInput.GetKeyDown("n"))
            {
                mycamera.fieldOfView = defaultFov;
            }
            if (SuntempleInput.GetKey(KeyCode.LeftShift))
            {
                moveSpeed = runSpeed;
            }
            else if (SuntempleInput.GetKey(KeyCode.LeftAlt))
            {
                moveSpeed = dashSpeed;
            }
            else
            {
                moveSpeed = baseSpeed;
            }



            if (SuntempleInput.GetAxis("Vertical") != 0 && SuntempleInput.GetAxis("Horizontal") != 0)
            {
                transform.position += transform.forward * moveSpeed / 1.5f * SuntempleInput.GetAxis("Vertical");
                transform.position += transform.right * moveSpeed / 1.5f * SuntempleInput.GetAxis("Horizontal");
            }
            else
            {
                transform.position += transform.forward * moveSpeed * SuntempleInput.GetAxis("Vertical");
                transform.position += transform.right * moveSpeed * SuntempleInput.GetAxis("Horizontal");
            }

            if (SuntempleInput.GetKey("space"))
            {
                transform.position += transform.up * moveSpeed * 0.15f;
            }
            if (SuntempleInput.GetKey(KeyCode.LeftControl))
            {
                transform.position += transform.up * moveSpeed * -0.15f;
            }
        }
    }
}
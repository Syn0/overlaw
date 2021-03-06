﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Classe permettant la rotation de la caemra autour d'un objet
/// </summary>
public class MNG_CameraController : MonoBehaviour {

    new public Camera camera;
    public GameObject camera_root;

    public Vector3 cameraOffset;
    public float cameraRotation;
    
    public float cameraVerticalClamp;

    public bool lockTransfomRotX = false;

    // Use this for initialization
    void Start ()
    {
        camera.transform.localPosition = cameraOffset;
        camera.transform.SetParent(camera_root.transform);
        updateCamera();
    }
	
	// Update is called once per frame
	void Update () {
        if(MNG_MainMenu.captureMouse) updateCamera();
    }

    void updateCamera()
    {
        

        //horizontal
        if(!lockTransfomRotX) transform.Rotate(transform.up, Input.GetAxis("Mouse X") * MNG_MainMenu.cameraRotationSpeed.x * Time.deltaTime);

        //vertical
        cameraRotation -= Input.GetAxis("Mouse Y")* MNG_MainMenu.cameraRotationSpeed.y * Time.deltaTime;
        if (cameraRotation > cameraVerticalClamp) cameraRotation = cameraVerticalClamp;
        if (cameraRotation < -cameraVerticalClamp) cameraRotation = -cameraVerticalClamp;
        //apply rotation
        camera_root.transform.localRotation = Quaternion.Euler(cameraRotation, 0, 0);
        //camera.transform.localRotation = Quaternion.Euler(0, 0, 0);
        
    }

    //**********************///

    void lockMouse(bool val)
    {
        if (val) Cursor.lockState = CursorLockMode.Confined;
        else Cursor.lockState = CursorLockMode.None;
    }
    bool isMouseLocked() { return Cursor.lockState == CursorLockMode.Confined ; }
    void setCursorActivation()
    {
        if (isMouseLocked() && Input.GetKeyDown("escape"))
            lockMouse(false);
        else if (!isMouseLocked() && Input.GetMouseButton(0)
            && ((Input.mousePosition.y > 0 && Input.mousePosition.y < Screen.height)
            || (Input.mousePosition.x > 0 && Input.mousePosition.x < Screen.width)))
            lockMouse(false);
    }


}

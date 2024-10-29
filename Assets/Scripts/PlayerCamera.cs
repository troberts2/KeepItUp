using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct CameraInput
{
    public Vector2 Look;
}

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private float sensitivity = .1f;
    [SerializeField] private float cameraMinX = -85f;
    [SerializeField] private float cameraMaxX = 85f;
    private Vector3 _eulerAngles;
    public void Initialize(Transform target)
    {
        transform.position = target.position;
        transform.eulerAngles = _eulerAngles = target.eulerAngles;
    }

    public void UpdateRotation(CameraInput input)
    {
        _eulerAngles += new Vector3(-input.Look.y, input.Look.x) * sensitivity;
        _eulerAngles.x = Mathf.Clamp(_eulerAngles.x, cameraMinX, cameraMaxX);
        transform.eulerAngles = _eulerAngles;
    }

    public void UpdatePosition(Transform target)
    {
        transform.position = target.position;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Ball : MonoBehaviour
{
    [SerializeField] private float ballUpForce = 10f;
    [SerializeField] private float upwardForce = -9.8f;
    private Rigidbody _rb;
    // Start is called before the first frame update
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if(Keyboard.current.eKey.wasPressedThisFrame)
        {
            _rb.AddForce(Vector3.up * ballUpForce, ForceMode.Impulse);
        }
    }

    private void FixedUpdate()
    {
        _rb.AddForce(Vector3.up * upwardForce, ForceMode.Acceleration);
    }
}

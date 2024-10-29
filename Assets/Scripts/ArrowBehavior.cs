using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ArrowBehavior : MonoBehaviour
{
    private Rigidbody rb;
    bool isStuck = false;
    [SerializeField] private LayerMask collidable;
    [SerializeField] private float _destoryAfterHitTime = 3f;
    private Quaternion impactRotation;
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update() {
        if(Physics.SphereCast(transform.position, 1f, transform.forward, out var hit, 1f, collidable) && impactRotation == null)
        {
            impactRotation = transform.rotation;
        }
        if(!isStuck)
        {
            transform.forward = rb.velocity;
        }
    }

    private void OnCollisionEnter(Collision other) {
        if(isStuck) return;
        //arrow should stop and get stuck in object
        // Get collision details
        ContactPoint contact = other.GetContact(0);

        // Stop the arrow's motion
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.mass = 0f;
        GetComponent<Collider>().enabled = false;

        // Parent to hit object if it has a transform (non-static objects)
        transform.SetParent(other.transform.root, true);

        // Align arrow with collision normal
        transform.position = contact.point;

        isStuck = true;

        Destroy(gameObject, _destoryAfterHitTime);
        
    }
}

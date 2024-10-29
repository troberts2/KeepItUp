using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraLean : MonoBehaviour
{
    [SerializeField] private float attackDamping = 0.5f;
    [SerializeField] private float decayDamping = .3f;
    [SerializeField] private float walkStrength = .075f;
    [SerializeField] private float slideStrength = .2f;
    [SerializeField] private float strengthResponse = 5f;

    private Vector3 _dampAcceleration;
    private Vector3 _dampAccelerationVel;

    private float _smoothStrength;

    public void Initialize()
    {
        _smoothStrength = walkStrength;
    }

    public void UpdateLean(float deltaTime, bool sliding, Vector3 acceleration, Vector3 up)
    {
        var planarAcceleration = Vector3.ProjectOnPlane(acceleration, up);
        var damping = planarAcceleration.magnitude > _dampAcceleration.magnitude
            ? attackDamping
            : decayDamping;

        _dampAcceleration = Vector3.SmoothDamp
        (
            _dampAcceleration,
            planarAcceleration,
            ref _dampAccelerationVel,
            damping,
            float.PositiveInfinity,
            deltaTime
        );

        var leanAxis = Vector3.Cross(_dampAcceleration.normalized, up).normalized;

        transform.localRotation = Quaternion.identity;

        var targetStrength = sliding
            ? slideStrength
            : walkStrength;

        _smoothStrength = Mathf.Lerp(_smoothStrength, targetStrength, 1f - Mathf.Exp(-strengthResponse * deltaTime));

        transform.rotation = Quaternion.AngleAxis(-_dampAcceleration.magnitude * _smoothStrength, leanAxis) * transform.rotation;
    }
}

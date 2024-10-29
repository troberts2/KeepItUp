using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class StanceVignette : MonoBehaviour
{
    [SerializeField] private float min = .1f;
    [SerializeField] private float max = .35f;
    [SerializeField] private float response = 10f;

    private VolumeProfile _profile;
    private Vignette _vignette;

    public void Initialize(VolumeProfile profile)
    {
        _profile = profile;

        if(!profile.TryGet(out _vignette))
        {
            _vignette = profile.Add<Vignette>();
        }

        _vignette.intensity.Override(min);
    }

    public void UpdateVignette(float deltaTime, Stance stance)
    {
        var targetIntensity = stance is Stance.Stand or Stance.WallRun ? min : max;
        _vignette.intensity.value = Mathf.Lerp
        (
            _vignette.intensity.value,
            targetIntensity,
            1f - Mathf.Exp(-response * deltaTime)
        );
    }
}

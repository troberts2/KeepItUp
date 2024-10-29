using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class Player : MonoBehaviour
{
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private BowShootingBehavior bowShootingBehavior;
    [Space]
    [SerializeField] private CameraSpring cameraSpring;
    [SerializeField] private CameraLean cameraLean;
    [Space]
    [SerializeField] private Volume volume;
    [SerializeField] private StanceVignette stanceVignette;

    private PlayerInputActions _inputActions;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        _inputActions = new PlayerInputActions();
        _inputActions.Enable();


        playerCharacter.Initialize();
        playerCamera.Initialize(playerCharacter.GetCameraTarget());
        cameraSpring.Initialize();
        cameraLean.Initialize();
        stanceVignette.Initialize(volume.profile);
    }

    private void OnDestroy() 
    {
        _inputActions.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        var input = _inputActions.Gameplay;
        var deltaTime = Time.deltaTime;

        //get character input and update it
        var characterInput = new CharacterInput 
        {
            Rotation = playerCamera.transform.rotation,
            Move = input.Move.ReadValue<Vector2>(),
            Jump = input.Jump.WasPressedThisFrame(),
            JumpSustain = input.Jump.IsPressed(),
            WallRun = input.WallRun.IsPressed(),
            Crouch = input.Crouch.WasPressedThisFrame()
                ? CrouchInput.Toggle
                : CrouchInput.None,
            Shoot = input.Shoot.IsPressed()
        };
        bowShootingBehavior.UpdateInput(characterInput);
        playerCharacter.UpdateInput(characterInput);
        playerCharacter.UpdateBody(deltaTime);

        #if UNITY_EDITOR
        if(Keyboard.current.tKey.wasPressedThisFrame)
        {
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out var hit))
            {
                Teleport(hit.point);
            }
        }
        #endif
    }

    private void LateUpdate() {
        var input = _inputActions.Gameplay;
        var deltaTime = Time.deltaTime;
        var cameraTarget = playerCharacter.GetCameraTarget();
        var state = playerCharacter.GetState();

        //Get camera input and update its rotation
        var cameraInput = new CameraInput { Look = input.Look.ReadValue<Vector2>() };
        playerCamera.UpdatePosition(cameraTarget);
        playerCamera.UpdateRotation(cameraInput);
        cameraSpring.UpdateSpring(deltaTime, cameraTarget.up);
        cameraLean.UpdateLean
        (
            deltaTime, 
            state.Stance is Stance.Slide, 
            state.Acceleration, 
            cameraTarget.up
        );

        stanceVignette.UpdateVignette(deltaTime, state.Stance);
    }

    public void Teleport(Vector3 position)
    {
        playerCharacter.SetPosition(position);
    }
}

using KinematicCharacterController;
using UnityEngine;
using UnityEngine.InputSystem;

public enum CrouchInput
{
    None, Toggle
}

public enum Stance
{
    Stand, Crouch, Slide, WallRun
}

public struct CharacterState
{
    public bool Grounded;
    public Stance Stance;
    public Vector3 Velocity;
    public Vector3 Acceleration;
}

public struct CharacterInput
{
    public Quaternion Rotation;
    public Vector2 Move;
    public bool Jump;
    public bool JumpSustain;
    public bool WallRun;
    public CrouchInput Crouch;
    public bool Shoot;
}

public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [SerializeField] private KinematicCharacterMotor motor;
    [SerializeField] private Transform root;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float walkSpeed = 20f;
    [SerializeField] private float crouchSpeed = 7f;
    [SerializeField] private float walkResponse = 25f;
    [SerializeField] private float crouchResponse = 20f;
    [Space]
    [SerializeField] private float jumpSpeed = 20f;
    [SerializeField] private float coyoteTime = .2f;
    [Range(0f, 1f)]
    [SerializeField] private float jumpSustainGravity = 0.4f;
    [SerializeField] private float gravity = -90f;
    [Space]
    [SerializeField] private float slideStartSpeed = 25f;
    [SerializeField] private float slideEndSpeed = 15f;
    [SerializeField] private float slideFriction = .08f;
    [SerializeField] private float slideSteerAcceleration = 5f;
    [SerializeField] private float slideGravity = -90f;
    [Space]
    [SerializeField] private LayerMask whatIsWall;
    [SerializeField] private float wallRunForce;
    [SerializeField] private float maxWallRunTime;
    [SerializeField] private float maxWallSpeed;
    private bool _isWallRight;
    private bool _isWallLeft;
    private bool _isWallForward;
    [SerializeField] private float maxWallCameraTilt;
    [SerializeField] private float wallRunCameraTilt;
    [Space]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchHeightResponse = 15f;
    [SerializeField] private float airSpeed = 15f;
    [SerializeField] private float airAcceleration = 70f;
    [Range(0f, 1f)]
    [SerializeField] private float standCameraTargetHeight = .9f;
    [Range(0f, 1f)]
    [SerializeField] private float crouchCameraTargetHeight = .2f;

    private CharacterState _state;
    private CharacterState _lastState;
    private CharacterState _tempState;

    private Vector3 _requestedMovement;
    private Quaternion _requestedRotation;
    private bool _requestedJump;
    private bool _requestedSustainedJump;
    private bool _requestedCrouch;
    private bool _requestedCrouchInAir;
    private bool _requestedWallRun;

    private float _timeSinceUngrounded;
    private float _timeSinceJumpRequest;
    private bool _ungroundedDueToJump;

    private Collider[] _uncrouchOverlapResults;

    public void Initialize()
    {
        _uncrouchOverlapResults = new Collider[8];

        _state.Stance = Stance.Stand;
        _lastState = _state;

        motor.CharacterController = this;
    }

    public void UpdateInput(CharacterInput input)
    {
        _requestedRotation = input.Rotation;
        _requestedMovement = new Vector3(input.Move.x, 0f, input.Move.y);
        //prevent faster diagonal movement
        _requestedMovement = Vector3.ClampMagnitude(_requestedMovement, 1f);
        //orient input so its relative to direction player is facing
        _requestedMovement = input.Rotation * _requestedMovement;

        var wasRequestingJump = _requestedJump;
        _requestedJump = _requestedJump || input.Jump;
        if(_requestedJump && !wasRequestingJump)
        {
            _timeSinceJumpRequest = 0f;
        }

        _requestedSustainedJump = input.JumpSustain;

        var wasRequestingCrouch = _requestedCrouch;
        _requestedCrouch = input.Crouch switch
        {
            CrouchInput.Toggle => !_requestedCrouch,
            CrouchInput.None => _requestedCrouch,
            _ => _requestedCrouch
        };
        if(_requestedCrouch && !wasRequestingCrouch)
        {
            _requestedCrouchInAir = !_state.Grounded;
        }
        else if(!_requestedCrouch && wasRequestingCrouch)
        {
            _requestedCrouchInAir = false;
        }

        _requestedWallRun = _requestedWallRun || input.WallRun;

    }

    public void UpdateBody(float deltaTime)
    {
        var currentHeight = motor.Capsule.height;
        var normalizedHeight = currentHeight / standHeight;

        var cameraTargetHeight = currentHeight * 
        (
            _state.Stance is Stance.Stand
                ? standCameraTargetHeight
                : crouchCameraTargetHeight
        );
        var rootTargetScale = new Vector3(1f, normalizedHeight, 1f);

        cameraTarget.localPosition = Vector3.Lerp
        (
            cameraTarget.localPosition,
            new Vector3(0f, cameraTargetHeight, 0f),
            1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
        );
        root.localScale = Vector3.Lerp
        (
            root.localScale,
            rootTargetScale,
            1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
        );
    }



        /// <summary>
        /// This is called when the motor wants to know what its rotation should be right now
        /// </summary>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime){
            var forward = Vector3.ProjectOnPlane
            (
                _requestedRotation * Vector3.forward,
                motor.CharacterUp
            );
            
            if(forward != Vector3.zero)
            {
                currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp);
            }
        }
        /// <summary>
        /// This is called when the motor wants to know what its velocity should be right now
        /// </summary>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            _state.Acceleration = Vector3.zero;

            if(motor.GroundingStatus.IsStableOnGround)
            {
                _timeSinceUngrounded = 0f;
                _ungroundedDueToJump = false;

                var groundedMovement = motor.GetDirectionTangentToSurface
                (
                    direction: _requestedMovement,
                    surfaceNormal: motor.GroundingStatus.GroundNormal
                ) * _requestedMovement.magnitude;
                //start sliding
                {
                    var moving = groundedMovement.sqrMagnitude > 0;
                    var crouching = _state.Stance is Stance.Crouch;
                    var wasStanding = _lastState.Stance is Stance.Stand;
                    var wasInAir = !_lastState.Grounded;
                    if(moving && crouching && (wasStanding || wasInAir))
                    {
                        _state.Stance = Stance.Slide;

                        if(wasInAir)
                        {
                            currentVelocity = Vector3.ProjectOnPlane
                            (
                                _lastState.Velocity,
                                motor.GroundingStatus.GroundNormal
                            );
                        }

                        var effectiveSlideStartSpeed = slideStartSpeed;
                        if(!_lastState.Grounded && !_requestedCrouchInAir)
                        {
                            effectiveSlideStartSpeed = 0f;
                            _requestedCrouchInAir = false;
                        }

                        var slideSpeed = Mathf.Max(effectiveSlideStartSpeed, currentVelocity.magnitude);
                        currentVelocity = motor.GetDirectionTangentToSurface
                        (
                            currentVelocity,
                            motor.GroundingStatus.GroundNormal
                        ) * slideSpeed;
                    }
                }
                //move
                if(_state.Stance is Stance.Stand or Stance.Crouch)
                {
                    var speed = _state.Stance is Stance.Stand
                        ? walkSpeed
                        : crouchSpeed;

                    var response = _state.Stance is Stance.Stand
                        ? walkResponse
                        : crouchResponse;

                    var targetVelocity = groundedMovement * speed;
                    var moveVelocity = Vector3.Lerp
                    (
                        currentVelocity,
                        targetVelocity,
                        1f - Mathf.Exp(-response * deltaTime)
                    );

                    _state.Acceleration = moveVelocity - currentVelocity;
                    currentVelocity = moveVelocity;
                }
                //continue sliding
                else if(_state.Stance is not Stance.WallRun)
                {
                    //friction
                    currentVelocity -= currentVelocity * (slideFriction * deltaTime);

                    //slope
                    {
                        var force = Vector3.ProjectOnPlane
                        (
                            -motor.CharacterUp,
                            motor.GroundingStatus.GroundNormal
                        ) * slideGravity;

                        currentVelocity -= force * deltaTime;
                    }

                    //steer
                    {
                        //target velocity is the plaeyrs movement direction at the current speed
                        var currentSpeed = currentVelocity.magnitude;
                        var targetVelocity = groundedMovement * currentSpeed;
                        var steerVelocity = currentVelocity;
                        var steerForce = (targetVelocity -currentVelocity) * slideSteerAcceleration * deltaTime;
                        
                        //add steer force but clamp velocity so slide doesnt incres due to direct movement input.
                        steerVelocity += steerForce;
                        steerVelocity = Vector3.ClampMagnitude(steerVelocity, currentSpeed);
                    
                        _state.Acceleration = (steerVelocity - currentVelocity) / deltaTime;

                        currentVelocity = steerVelocity;
                    }
                    //stop
                    if(currentVelocity.magnitude < slideEndSpeed)
                    {
                        _state.Stance = Stance.Crouch;
                    }
                }
            }
            //else in air
            else
            {
                _timeSinceUngrounded += deltaTime;

                //wall run
                if(_state.Stance is Stance.WallRun)
                {
                    _state.Stance = Stance.WallRun;
                    
                    currentVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
                
                    if(currentVelocity.magnitude <= maxWallSpeed)
                    {
                        if(!_isWallForward)
                            currentVelocity += motor.CharacterForward * wallRunForce * deltaTime;
                        else
                            currentVelocity += motor.CharacterUp * wallRunForce * deltaTime;

                        //make sure char sticks to wall
                        if(_isWallRight)
                        {
                            currentVelocity += motor.CharacterRight * wallRunForce / 5 * deltaTime;
                        }
                        else if(_isWallLeft)
                        {
                            currentVelocity += -motor.CharacterRight * wallRunForce / 5 * deltaTime;
                        }
                        else
                        {
                            currentVelocity += motor.CharacterForward * wallRunForce / 5 * deltaTime;
                        }
                    }
                }
                //move
                else if(_requestedMovement.sqrMagnitude > 0)
                {
                    var planarMovement = Vector3.ProjectOnPlane
                    (
                        _requestedMovement,
                        motor.CharacterUp
                    ).normalized * _requestedMovement.magnitude;

                    var currentPlanarVelocity = Vector3.ProjectOnPlane
                    (
                        currentVelocity,
                        motor.CharacterUp
                    );

                    var movementForce = planarMovement * airAcceleration * deltaTime;

                    if(currentPlanarVelocity.magnitude < airSpeed)
                    {
                        var targetPlanarVelocity = currentPlanarVelocity + movementForce;

                        targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed); 

                        movementForce = targetPlanarVelocity - currentPlanarVelocity;
                    }
                    //otherwise nerf the movement force when it is in the direction of the current planar velocity
                    //to prevent accelerating further beyond the max air speed
                    else if(Vector3.Dot(currentPlanarVelocity, movementForce) > 0f)
                    {
                        //project movement force onto plane whose normal is current planar velocity
                        var constrainedMovementForce = Vector3.ProjectOnPlane
                        (
                            movementForce,
                            currentPlanarVelocity.normalized
                        );

                        movementForce = constrainedMovementForce;
                    }

                    //prevent air-climbing steep slopes
                    if(motor.GroundingStatus.FoundAnyGround)
                    {
                        //if moving in same direction as resultant velocity
                        if(Vector3.Dot(movementForce, currentVelocity + movementForce) > 0f)
                        {
                            //calculate obstruction normal.
                            var obtructionNormal = Vector3.Cross
                            (
                                motor.CharacterUp,
                                motor.GroundingStatus.GroundNormal
                            ).normalized;

                            //project movement force onto obstruction plane.
                            movementForce = Vector3.ProjectOnPlane(movementForce, obtructionNormal);
                        }
                    }


                    currentVelocity += movementForce;
                }
                
                //gravity
                if(_state.Stance is not Stance.WallRun)
                {
                    var effectiveGravity = gravity;
                    var verticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
                    if(_requestedSustainedJump && verticalSpeed > 0f) effectiveGravity *= jumpSustainGravity;

                    currentVelocity += motor.CharacterUp * effectiveGravity * deltaTime;
                }
                
            
            }

            if(_requestedJump)
            {
                var grounded = motor.GroundingStatus.IsStableOnGround;
                var canCoyoteJump = _timeSinceUngrounded < coyoteTime && !_ungroundedDueToJump;

                if(grounded || canCoyoteJump)
                {
                    _requestedJump = false; // uset jump
                    _requestedCrouch = false; // and request the character uncrouches
                    _requestedCrouchInAir = false;

                    motor.ForceUnground(time:0f);
                    _ungroundedDueToJump = true;

                    //set minimum vertical speed to jump speed
                    var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
                    var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                    //add the difference in current and target vertical speed to player velocity
                    currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
                }
                else if(_state.Stance is Stance.WallRun)
                {
                    //normal jump
                    if(_isWallLeft && !Keyboard.current.dKey.isPressed || _isWallRight && !Keyboard.current.aKey.isPressed  || _isWallForward && !Keyboard.current.wKey.isPressed)
                    {
                        _requestedJump = false; // uset jump
                        _requestedCrouch = false; // and request the character uncrouches
                        _requestedCrouchInAir = false;
                        _state.Stance = Stance.Stand;

                        motor.ForceUnground(time:0f);
                        _ungroundedDueToJump = true;

                        //set minimum vertical speed to jump speed
                        var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
                        var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                        //add the difference in current and target vertical speed to player velocity
                        currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed) * 1.5f;
                    }
                    //side wallhope
                    if(_isWallRight && Keyboard.current.aKey.isPressed)
                    {
                        currentVelocity += -motor.CharacterRight * jumpSpeed * .5f;
                        currentVelocity += motor.CharacterUp * jumpSpeed * 1.5f;
                    }
                    if(_isWallLeft && Keyboard.current.dKey.isPressed)
                    {
                        currentVelocity += motor.CharacterRight * jumpSpeed * .5f;
                        currentVelocity += motor.CharacterUp * jumpSpeed * 1.5f;
                    }
                    //back hop
                    if(_isWallForward && Keyboard.current.wKey.isPressed)
                    {
                        currentVelocity += motor.CharacterForward * jumpSpeed * .5f;
                        currentVelocity += motor.CharacterUp * jumpSpeed * 1.5f;
                    }
                }
                else
                {
                    _timeSinceJumpRequest += deltaTime;

                    var canJumpLater = _timeSinceJumpRequest < coyoteTime;
                    _requestedJump = canJumpLater;
                }

            }
            
        }
        /// <summary>
        /// This is called before the motor does anything
        /// </summary>
        public void BeforeCharacterUpdate(float deltaTime)
        {
            _tempState = _state;


            //crouch
            if(_requestedCrouch && _state.Stance is Stance.Stand)
            {
                _state.Stance = Stance.Crouch;
                motor.SetCapsuleDimensions
                (
                    radius: motor.Capsule.radius,
                    height: crouchHeight,
                    yOffset: crouchHeight * .5f
                );
            }

            //check for wall
            _isWallForward = Physics.Raycast(transform.position, motor.CharacterForward, 1f, whatIsWall);
            _isWallRight = Physics.Raycast(transform.position, motor.CharacterRight, 1f, whatIsWall);
            _isWallLeft = Physics.Raycast(transform.position, -motor.CharacterRight, 1f, whatIsWall);

            //start wall run
            bool wallRunForward = _isWallForward && Keyboard.current.wKey.isPressed;
            bool wallRunRight = _isWallRight && Keyboard.current.dKey.isPressed;
            bool wallRunLeft = _isWallLeft && Keyboard.current.aKey.isPressed;
            if((wallRunLeft || wallRunRight || wallRunForward) && !motor.GroundingStatus.IsStableOnGround)
            {
                _state.Stance = Stance.WallRun;
            }

            //leave wall run
            if(!_isWallLeft && !_isWallRight && !_isWallForward && _state.Stance is Stance.WallRun)
            {
                _state.Stance = Stance.Stand;
            }

            //add double jump here when implemented
        }
        /// <summary>
        /// This is called after the motor has finished its ground probing, but before PhysicsMover/Velocity/etc.... handling
        /// </summary>
        public void PostGroundingUpdate(float deltaTime)
        {
            if(!motor.GroundingStatus.IsStableOnGround && _state.Stance is Stance.Slide)
            {
                _state.Stance = Stance.Crouch;
            }
        }
        /// <summary>
        /// This is called after the motor has finished everything in its update
        /// </summary>
        public void AfterCharacterUpdate(float deltaTime)
        {
            //uncrouch
            if(!_requestedCrouch && _state.Stance is not Stance.Stand)
            {
                //tentatively "standup" character capsule
                motor.SetCapsuleDimensions
                (
                    radius: motor.Capsule.radius,
                    height: standHeight,
                    yOffset: standHeight * .5f
                );

                //then see if capsule overlaps any colliders befor
                //actually allowing chracter to stand
                var pos = motor.TransientPosition;
                var rot = motor.TransientRotation;
                var mask = motor.CollidableLayers;
                if(motor.CharacterOverlap(pos, rot, _uncrouchOverlapResults, mask, QueryTriggerInteraction.Ignore) > 0)
                {
                    //recrouch
                    _requestedCrouch = true;
                    motor.SetCapsuleDimensions
                    (
                        radius: motor.Capsule.radius,
                        height: crouchHeight,
                        yOffset: crouchHeight * .5f
                    );
                }
                else
                {
                    if(_state.Stance is not Stance.WallRun)
                    {
                        _state.Stance = Stance.Stand;
                    }
                    
                }
            }

            var totalAcceleration = (_state.Velocity - _lastState.Velocity) / deltaTime;
            _state.Acceleration = Vector3.ClampMagnitude(_state.Acceleration, totalAcceleration.magnitude);

            _state.Grounded = motor.GroundingStatus.IsStableOnGround;
            _state.Velocity = motor.Velocity;
            _lastState = _tempState;
        }
        /// <summary>
        /// This is called after when the motor wants to know if the collider can be collided with (or if we just go through it)
        /// </summary>
        public bool IsColliderValidForCollisions(Collider coll){
            return true;
        }
        /// <summary>
        /// This is called when the motor's ground probing detects a ground hit
        /// </summary>
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport){

        }
        /// <summary>
        /// This is called when the motor's movement logic detects a hit
        /// </summary>
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
             _state.Acceleration = Vector3.ProjectOnPlane(_state.Acceleration, hitNormal);
        }
        /// <summary>
        /// This is called after every move hit, to give you an opportunity to modify the HitStabilityReport to your liking
        /// </summary>
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport){

        }
        /// <summary>
        /// This is called when the character detects discrete collisions (collisions that don't result from the motor's capsuleCasts when moving)
        /// </summary>
        public void OnDiscreteCollisionDetected(Collider hitCollider){

        }


    public Transform GetCameraTarget() => cameraTarget;
    public CharacterState GetState() => _state;
    public CharacterState GetLastState() => _lastState;
    public void SetPosition(Vector3 position, bool killVelocity = true)
    {
        motor.SetPosition(position);
        if(killVelocity)
        {
            motor.BaseVelocity = Vector3.zero;
        }
    }
}

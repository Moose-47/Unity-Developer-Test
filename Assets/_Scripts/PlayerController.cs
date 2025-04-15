using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, PlayerControls.IPlayerActions
{
    #region Player Character Variables
    [Header("Movement")]
    [SerializeField] private float maxSpeed = 5f; //How fast the player moves at top speed
    [SerializeField] private float accel = 10f; //How quickly the player accelerates when moving
    [SerializeField] private float decel = 15f; //How quickly the player decelerates after they stop moving
    [SerializeField] private float turnSpeed = 10f; //How quickly the player turns to face the movement direction

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f; //How high the player can jump
    [SerializeField] private float jumpDuration = 0.5f; //How long the jump lasts, the player reaches the jumpHeight at the end of this duration
    private float maxFallSpeed = -20f; //Clamps the fall speed to prevent the player from falling 'too fast'

    [Header("References")]
    public Transform cameraTransform;

    private CharacterController controller;
    private PlayerControls input;

    private Vector2 moveInput; //Stores the current movement direction (WASD)
    private bool jumpPressed; //Stores the jump state

    private Vector3 currentVelocity; //Horizontal Velocity
    private float verticalVelocity; //Vertical Velocity

    //So the player moves with the platform instead of the platform sliding out from beneath the player
    private Vector3 platformVelocity = Vector3.zero;
    private MovingPlatform currentPlatform = null;

    private float gravity; //Dynamically calculated below
    private float jumpVelocity; //Calculated below using jumpHeight and jumpDuration

    private float coyoteTime = 0.15f; //Grace period where player can still jump after stepping off of a platform
    private float jumpBufferTime = 0.15f; //Period of time in which the jump input is stored before player touches the ground
    private float coyoteTimer;
    private float jumpBufferTimer;
    #endregion
    void Awake()
    {
        #region Player Controller Initialization
        //Initializes the input system and sets this script as the callback handler
        controller = GetComponent<CharacterController>();
        input = new PlayerControls();
        input.Player.SetCallbacks(this);
        input.Player.Enable();
        #endregion

        //Gravity calculation derived from h = 1/2 * g * t^2. Ex: jumpHeight = 4, jumpDuration = 0.8.
        //gravity = -(2 * 4) / (0.8 / 2)^2
                //= -8 / (0.4)^2
                //= -8 / 0.16 = -50
        gravity = -(2 * jumpHeight) / Mathf.Pow(jumpDuration / 2f, 2); 
       
        jumpVelocity = Mathf.Abs(gravity) * (jumpDuration / 2f); //Calculating jumpVelocity, v = g * t (velocity = gravity * time)
    }

    void Update()
    {
        SetGrounded(controller.isGrounded); //Checking if grounded
        HandleTimers(); //Handling jump buffer and coyote time

        if (controller.isGrounded)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.1f)) //casting a ray down checking for objects
            {
                //If the object hit has the MovingPlatforms script it sets the velocity to move the player with the platform
                currentPlatform = hit.collider.GetComponent<MovingPlatform>(); 
                platformVelocity = currentPlatform ? currentPlatform.Velocity : Vector3.zero;
            }
            else
            {
                currentPlatform = null;
                platformVelocity = Vector3.zero;
            }
        }
        else platformVelocity = Vector3.zero;

        Vector3 moveDirection = GetCameraRelativeInput(moveInput); //Converts input into camera-relative movement
        HandleRotation(moveDirection); //Rotates player towards movement direction
        HandleMovement(moveDirection, moveInput.magnitude > 0.1f); //Moves the player
        HandleJump(); //Handles jumping

        verticalVelocity += gravity * Time.deltaTime; //Applies gravity to vertical movement
        verticalVelocity = Mathf.Max(verticalVelocity, maxFallSpeed); //Clamps vertical speed to maxFallSpeed

        Vector3 finalVelocity = currentVelocity + Vector3.up * verticalVelocity + platformVelocity; //Combines horizontal, vertical movement and platform movement
        controller.Move(finalVelocity * Time.deltaTime); //applies the above line with CharacterController.Move
    }

    private void HandleTimers() //Resets and decreases timers for jump buffer and coyote time
    {
        if (controller.isGrounded) 
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.deltaTime; 

        jumpBufferTimer -= Time.deltaTime; 
    }

    private Vector3 GetCameraRelativeInput(Vector2 input) //Converts 2D input into 3D movement relative to the camera's yaw (ignoring pitch and roll)
    {
        if (cameraTransform == null) return Vector3.zero;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        //Zeroing the Y values to prevent the player from moving up and down, locking movement to the X and Z plane regradless of camera tilt
        forward.y = 0;
        right.y = 0;

        //Ensuring the vectors have a length of 1 to prevent weird movement scaling
        forward.Normalize();
        right.Normalize();

        //input.y is the vertical input (W/S), input.x is horizontal input (A/D), forward direction * vertical input and right direction * horizontal input
        //adding these vectors together gets the direction the player should move relative to where the camera is facing
        //normalized ensures that diagonal input (W+D) doesnt move the player faster than (W) or (D) does on their on
        //like if a square is 1x1, moving left/right or up/down is 1 but diagonal is 1.41
        return (forward * input.y + right * input.x).normalized;
    }

    private void HandleRotation(Vector3 moveDirection) //Rotates the player smoothly toward their movement direction using Slerp (Spherical Linear Interpolation)
        //used to smoothly rotate one direction to another over time. Quaternion.Slerp(starting rotation, target rotation, interpolation amount)
    {
        if (moveDirection.magnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private void HandleMovement(Vector3 direction, bool isMoving) //Applied acceleration/deceleration to smooth movement
    {
        Vector3 desiredVelocity = direction * maxSpeed; //Multiplying input direction by the maxSpeed
        float usedAccel = controller.isGrounded ? accel : accel / 2f; //If player is grounded use full acceleration, if they're airborn use half the acceleration

        if (isMoving) 
            currentVelocity = Vector3.MoveTowards(currentVelocity, desiredVelocity, usedAccel * Time.deltaTime); //Insuring that acceleration is gradual and smooth
        else
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, decel * Time.deltaTime); //Insuring that deceleration is gradual and smooth
    }

    private void HandleJump()
    {
        if (jumpBufferTimer > 0 && coyoteTimer > 0) //Performs jump if within the buffer or coyote time window or while grounded
        {
            verticalVelocity = jumpVelocity;
            jumpBufferTimer = 0;
            coyoteTimer = 0;
        }

        if (!jumpPressed && verticalVelocity > 0)  //Ends the upward velocity of the jump if jump button released early (variable jump height)
            verticalVelocity *= 0.5f;
    }

    public void SetGrounded(bool grounded) //Resets fall speed when grounded
    {
        if (grounded && verticalVelocity < 0)
            verticalVelocity = 0;
    }

    //These respond to the input system (IPlayerActions) and updates the movement and jump state
    #region Movement and Jump control
    public void OnMovement(InputAction.CallbackContext context) 
    {
        moveInput = context.ReadValue<Vector2>();
    }
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            jumpPressed = true;
            jumpBufferTimer = jumpBufferTime;
        }
        else if (context.canceled)
            jumpPressed = false;
    }
    #endregion
}

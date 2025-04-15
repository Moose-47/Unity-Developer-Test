using UnityEngine;

public class CameraControls : MonoBehaviour
{
    public Transform player;
    #region Camera Variables
    [Header("Camera Sensitivity")] 
    public float sensitivityX = 10f;
    public float sensitivityY = 10f;

    [Header("Vertical View Angle")] //Limits the amount the camera can tilt vertically
    public float minY = -5f;
    public float maxY = 60f;

    [Header("Camera Placement")]
    public float defaultDistance = 5f; //How far behind the player the camera sits
    public float cameraHeightOffset = 1.5f; //How high the camera pivot is relative to the player

    [Header("Smoothing Speed and Obstruction")]
    public float smoothSpeed = 10f; //How fast the camera moves to its new position
    public float verticalSmoothSpeed = 5f; //How quickly the pivot point adjust vertically 
    public float obstructionSmoothSpeed = 15f; //How fast the camera adjust if something is blocking its view
    public LayerMask obstructionMask; //To define which layer the raycast will check for camera obstructing geometry

    [Header("Camera Deadzone")]
    public float deadzoneThreshold = 0.1f; //Prevents small movements from triggering the camera motion

    private float rotationX = 0f; //Tracks how much the player has moved the camera left/right
    private float rotationY = 0f; //Tracks how much the player has moved the camera up/down
    private Vector3 currentCameraPos; //Actual position of the camera (used for smoothing)
    private Vector3 smoothedPivot; //Smoother version of the players position (reduces sudden height changes)
    private Transform cameraTransform; 
    private float currentDistance; //Distance camera should stay from the pivot (adjust when obstructed)
    #endregion
    void Start()
    {
        cameraTransform = Camera.main.transform; //Referencing main camera
        
        //Initializing the distance, position, and pivot point of the camera
        currentDistance = defaultDistance;
        currentCameraPos = transform.position;
        smoothedPivot = player.position + Vector3.up * cameraHeightOffset;
      
        Cursor.lockState = CursorLockMode.Locked; //Locking the mouse cursor
    }

    void Update() //Update the camera's rotation and position each frame
    {
        HandleRotation();
        UpdateCameraPosition();
    }

    void HandleRotation() //Reads mouse inputs, handling the rotation of the camera
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivityX; 
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY;

        rotationY += mouseX; //positive mouseX = increased rotationY = turn right | negative mouseX = decreased rotationY = turn left
        rotationX -= mouseY; //positive mouseY = decreased rotationX = look up | negative mouseY = increased rotationX = look down
        rotationX = Mathf.Clamp(rotationX, minY, maxY); //Clamping the vertical angle the camera can be tilted at

        //Quaternian.Euler(x, y, z) rotationX = pitch (look up/down), rotationY = yaw (turn left/right), 0 = roll (tilt side to side)
        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0); 
    }

    void UpdateCameraPosition()
    {
        Vector3 rawPivot = player.position + Vector3.up * cameraHeightOffset; //calculates where the camera wants to pivot based on the players height

        //Smoothly interpolate between the previous and new pivot position to reduce jitter (especially useful for uneven terrain and stairs)
        smoothedPivot = Vector3.Lerp(smoothedPivot, rawPivot, Time.deltaTime * verticalSmoothSpeed);

        //Calculating where the camera should be if there are no obstacles
        Vector3 desiredCameraPos = smoothedPivot - transform.forward * defaultDistance;

        //Using raycast to check for obstacles between the player and the camera
        if (Physics.Raycast(smoothedPivot, -transform.forward, out RaycastHit hit, defaultDistance, obstructionMask))
        {
            //If an obstacle is hit the camera moves closer to avoid clipping into the obstacle
            currentDistance = Mathf.Lerp(currentDistance, hit.distance - 0.1f, Time.deltaTime * obstructionSmoothSpeed);
        }
        else
        {
            //If nothing is hit the camera moves back out to its default distance, using Lerp to smooth this transition
            currentDistance = Mathf.Lerp(currentDistance, defaultDistance, Time.deltaTime * obstructionSmoothSpeed);
        }

        //Apply the adjusted distance to get the final target position for the camera
        Vector3 targetCameraPos = smoothedPivot - transform.forward * currentDistance;

        //Deadzone logic
        float distanceToTarget = Vector3.Distance(currentCameraPos, targetCameraPos); //Checking if the new target camera position is far enough away from the current
        if (distanceToTarget > deadzoneThreshold)
        {
            //If outside the deadzone the camera is smoothly moved to the new position. Lerp to help prevent jittery camera snapping
            currentCameraPos = Vector3.Lerp(currentCameraPos, targetCameraPos, Time.deltaTime * smoothSpeed);
        }

        cameraTransform.position = currentCameraPos; //Apply the final smoothed position to the actual camera
        cameraTransform.LookAt(smoothedPivot); //Rotate the camera so it always looks at the pivot
    }
}


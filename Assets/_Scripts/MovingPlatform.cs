using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    //Positions in the world space that the platform moves between
    public Transform pointA;
    public Transform pointB;

    [SerializeField] private float moveSpeed = 2f; //How fast the platform moves
    [SerializeField] private float waitTime = 2f; //How long the platform waits at each point

    private Vector3 currentTarget; //Stores whether the platform is heading towards pointA or pointB
    private float waitTimer = 0f; //Cooldown for the delay at each point
    private bool isWaiting = false; //Tracks whether the platform is waiting or not

    private Vector3 previousPosition; //Used to calculate velocity (stores last frames position)
    private Vector3 platformVelocity; //Calculate velocity of the platform (used for influencing the player movement)

    public Vector3 Velocity => platformVelocity; //Makes platformVelocity accessible to other scripts

    void Start()
    {
        currentTarget = pointB.position; //At start the platform will move towards pointB
        previousPosition = transform.position; //Store current position to use in velocity calculation later
    }

    void Update()
    {
        //Calculate velocity before movement
        platformVelocity = (transform.position - previousPosition) / Time.deltaTime;
        previousPosition = transform.position; //Saving the current frame position for the next frames velocity calculatoin

        if (isWaiting) //Stops movement if the platform is waiting at either point
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                //If waiting is false the platform starts to move again flipping its currentTarget to the other point
                currentTarget = currentTarget == pointA.position ? pointB.position : pointA.position;
            }
            return;
        }

       
        Vector3 direction = (currentTarget - transform.position).normalized; //Calculate normalized direction vector point from the platform to the target
        transform.position += direction * moveSpeed * Time.deltaTime; //Moves the platform along the vector based on the movement speed of the platform
       
        if (Vector3.Distance(transform.position, currentTarget) < 0.05f) //If platform is within range of either point it will start its waiting countdown
        {
            isWaiting = true;
            waitTimer = waitTime;
        }
    }
}

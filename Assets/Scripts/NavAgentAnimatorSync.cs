using UnityEngine;
using UnityEngine.AI;

public class NavAgentAnimatorSync : MonoBehaviour
{
    private Animator animator;
    private NavMeshAgent agent;
    public float moveThreshold = 0.1f;
    public float turnSpeed = 120f;
    public float arrivalThreshold = 0.5f; // Distance to consider "arrived"

    void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // Important for root motion approach
        agent.updatePosition = false; // Animation controls position
        agent.updateRotation = false; // We control rotation
    }

    void Update()
    {
        // Check if close enough to destination to stop
        bool hasPath = agent.hasPath && agent.remainingDistance > 0;
        bool isCloseToDestination = hasPath && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, arrivalThreshold);
        
        // Set animation based on agent velocity and proximity to destination
        bool shouldWalk = agent.velocity.magnitude > moveThreshold && !isCloseToDestination;
        animator.SetBool("isWalking", shouldWalk);
        
        // Stop the agent when arrived
        if (isCloseToDestination && !agent.isStopped)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // Handle rotation (not controlled by animation or agent)
        AgentDestinationSetter destSetter = GetComponent<AgentDestinationSetter>();
        bool destinationSetterIsRotating = destSetter != null && agent.isStopped;

        if (!destinationSetterIsRotating && agent.velocity.sqrMagnitude > moveThreshold * moveThreshold)
        {
            Quaternion targetRotation = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }
    }

    void OnAnimatorMove()
    {
        if (animator.GetBool("isWalking"))
        {
            // Match agent speed to animation movement speed
            agent.speed = (animator.deltaPosition / Time.deltaTime).magnitude;
            
            // Apply animation root motion position
            Vector3 newPosition = transform.position + animator.deltaPosition;
            
            // Keep on NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(newPosition, out hit, 1.0f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
            else
            {
                transform.position = newPosition;
            }
            
            agent.nextPosition = transform.position;
        }
    }
}

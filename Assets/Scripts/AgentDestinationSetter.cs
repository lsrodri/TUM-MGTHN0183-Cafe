using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AgentDestinationSetter : MonoBehaviour
{
    public Transform destination;
    public Transform vrCamera;
    public float delayBeforeWalkingToDestination = 2f;
    public float delayBeforeReturning = 2f;
    public float arrivalRotationDistance = 2f;
    public string turnRightAnimationName = "TurnRight";
    public string turnLeftAnimationName = "TurnLeft";
    public string talkingAnimationName = "Talking";
    public float turnSpeed = 180f;
    public float talkingDuration = 3f;
    public bool startSequenceOnStart = true; // NEW: Control if sequence runs at start

    private NavMeshAgent agent;
    private Animator animator;
    private Transform origin;
    private bool shouldFaceCamera = false;
    private bool isReturningToOrigin = false;
    private Coroutine currentSequence; // NEW: Track current sequence

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (vrCamera == null)
        {
            vrCamera = Camera.main?.transform;
            if (vrCamera == null)
            {
                Debug.LogWarning("VR Camera not found. Please assign it in the Inspector.");
            }
        }

        GameObject originMarker = new GameObject("Origin_" + gameObject.name);
        originMarker.transform.position = transform.position;
        originMarker.transform.rotation = transform.rotation;
        origin = originMarker.transform;

        // Only start sequence automatically if enabled
        if (startSequenceOnStart)
        {
            StartWalkSequence();
        }
    }

    void Update()
    {
        if (shouldFaceCamera && vrCamera != null && agent.enabled && !agent.isStopped)
        {
            if (agent.remainingDistance <= arrivalRotationDistance && agent.remainingDistance > agent.stoppingDistance)
            {
                RotateTowardsCamera();
            }
        }
    }

    // NEW: Public method to start the sequence (can be called from anywhere)
    public void StartWalkSequence()
    {
        // Stop any existing sequence first
        if (currentSequence != null)
        {
            StopCoroutine(currentSequence);
            ResetAgentState();
        }

        currentSequence = StartCoroutine(FullWalkSequence());
    }

    // NEW: Public method to stop the current sequence
    public void StopWalkSequence()
    {
        if (currentSequence != null)
        {
            StopCoroutine(currentSequence);
            currentSequence = null;
            ResetAgentState();
        }
    }

    // NEW: Reset agent to clean state
    private void ResetAgentState()
    {
        agent.isStopped = true;
        agent.ResetPath();
        shouldFaceCamera = false;
        isReturningToOrigin = false;
        
        if (animator != null)
        {
            animator.SetBool("isWalking", false);
            animator.SetBool("isTurningRight", false);
            animator.SetBool("isTurningLeft", false);
            animator.SetBool("isTalking", false);
        }
    }

    private IEnumerator FullWalkSequence()
    {
        // Wait initial delay
        yield return new WaitForSeconds(delayBeforeWalkingToDestination);

        // Walk to destination
        yield return StartCoroutine(WalkToDestinationCoroutine(destination, true));

        // Wait until arrived at destination
        yield return StartCoroutine(WaitForArrival());

        // Play talking animation
        yield return StartCoroutine(PlayTalkingAnimation());

        // Wait in idle before returning (breathing animation plays)
        yield return new WaitForSeconds(delayBeforeReturning);

        // Turn left and walk back to origin
        isReturningToOrigin = true;
        yield return StartCoroutine(TurnAndWalkTo(origin, false, turnLeftAnimationName));

        // Wait until arrived back at origin
        yield return StartCoroutine(WaitForArrival());

        isReturningToOrigin = false;
        currentSequence = null; // Mark sequence as complete
        Debug.Log("Sequence complete!");
    }

    private IEnumerator WaitForArrival()
    {
        while (!agent.hasPath || agent.pathPending)
        {
            yield return null;
        }

        while (agent.remainingDistance > Mathf.Max(agent.stoppingDistance, 0.5f))
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator PlayTalkingAnimation()
    {
        if (animator != null)
        {
            animator.SetBool("isTalking", true);
            float duration = talkingDuration;
            yield return new WaitForSeconds(duration);
            animator.SetBool("isTalking", false);
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void RotateTowardsCamera()
    {
        Vector3 directionToCamera = vrCamera.position - transform.position;
        directionToCamera.y = 0;

        if (directionToCamera.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
            NavAgentAnimatorSync animSync = GetComponent<NavAgentAnimatorSync>();
            float rotSpeed = animSync != null ? animSync.turnSpeed : 120f;
            
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotSpeed * Time.deltaTime
            );
        }
    }

    private IEnumerator WalkToDestinationCoroutine(Transform target, bool faceCamera)
    {
        shouldFaceCamera = faceCamera;
        yield return StartCoroutine(TurnAndWalkTo(target, faceCamera, turnRightAnimationName));
    }

    public void WalkTo(Transform target, bool faceCamera = false)
    {
        if (target != null && agent != null)
        {
            shouldFaceCamera = faceCamera;
            StartCoroutine(TurnAndWalkTo(target, faceCamera, turnRightAnimationName));
        }
    }

    public void WalkToOrigin()
    {
        StartCoroutine(TurnAndWalkTo(origin, false, turnLeftAnimationName));
    }

    private IEnumerator TurnAndWalkTo(Transform target, bool faceCamera, string turnAnimationName)
    {
        agent.isStopped = true;
        agent.ResetPath();

        Vector3 directionToTarget = target.position - transform.position;
        directionToTarget.y = 0;

        if (directionToTarget.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            
            // NEW: Use the requested animation, not the calculated shortest path
            bool requestedTurnLeft = turnAnimationName == turnLeftAnimationName;
            string animBoolName = requestedTurnLeft ? "isTurningLeft" : "isTurningRight";

            if (animator != null)
            {
                animator.SetBool(animBoolName, true);
            }

            float turnDuration = GetAnimationClipLength(turnAnimationName);
            float elapsed = 0f;

            while (elapsed < turnDuration || Quaternion.Angle(transform.rotation, targetRotation) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime
                );

                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.rotation = targetRotation;

            if (animator != null)
            {
                animator.SetBool(animBoolName, false);
            }
            
            yield return new WaitForSeconds(0.1f);
        }

        agent.isStopped = false;
        agent.SetDestination(target.position);
    }

    private float GetAnimationClipLength(string clipName)
    {
        if (animator == null) return 0f;

        RuntimeAnimatorController ac = animator.runtimeAnimatorController;
        foreach (AnimationClip clip in ac.animationClips)
        {
            if (clip.name == clipName)
            {
                return clip.length;
            }
        }

        Debug.LogWarning($"Animation clip '{clipName}' not found. Using default duration.");
        return 0.5f;
    }
}
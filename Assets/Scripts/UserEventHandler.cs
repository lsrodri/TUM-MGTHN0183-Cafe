using UnityEngine;
using UnityEngine.InputSystem;

public class UserEventHandler : MonoBehaviour
{
    public AgentDestinationSetter agentController;

    void Update()
    {
        // New Input System: Press 'R' to restart sequence
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            agentController.StartWalkSequence();
            
            // Temporary feedback on console for waiter return
            Debug.Log("Walk sequence restarted by user.");
        }
    }
}
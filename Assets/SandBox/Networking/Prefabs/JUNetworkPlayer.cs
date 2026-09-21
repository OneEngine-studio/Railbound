using Unity.Netcode;
using UnityEngine;
using JUTPS;
using JUTPS.CameraSystems;
using JUTPS.JUInputSystem;

public class JUNetworkPlayer : NetworkBehaviour
{
    private JUCharacterController characterController;
    private Rigidbody rb;
    private JUInputManager inputManager;

    private void Awake()
    {
        characterController = GetComponent<JUCharacterController>();
        rb = GetComponent<Rigidbody>();
        inputManager = GetComponent<JUInputManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // LOCAL PLAYER: Enable physics and JU TPS input logic
            if (inputManager != null) inputManager.enabled = true;
            if (characterController != null) characterController.enabled = true;
            if (rb != null) rb.isKinematic = false;

            TPSCameraController tpsCamera = FindFirstObjectByType<TPSCameraController>(FindObjectsInactive.Include);
            if (tpsCamera != null && characterController != null)
            {
                tpsCamera.TargetToFollow = transform;
                tpsCamera.characterTarget = characterController;
                tpsCamera.enabled = true;
            }
        }
        else
        {
            // REMOTE CLONE: Hard-disable JU TPS logic so inputs do not leak.
            // Netcode ClientNetworkTransform and ClientNetworkAnimator will move this puppet.
            if (inputManager != null) inputManager.enabled = false;
            if (characterController != null) characterController.enabled = false; 
            if (rb != null) rb.isKinematic = true; 
        }
    }
}
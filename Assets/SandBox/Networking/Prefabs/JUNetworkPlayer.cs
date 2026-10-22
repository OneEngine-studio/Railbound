using Unity.Netcode;
using UnityEngine;
using JUTPS;
using JUTPS.CameraSystems;
using JUTPS.JUInputSystem;
using JUTPS.InventorySystem;
using JUTPS.InventorySystem.UI;

public class JUNetworkPlayer : NetworkBehaviour
{
    private JUCharacterController characterController;
    private Rigidbody rb;
    private JUInputManager inputManager;
    private JUHealth health;
    private JUInventory inventory;

    private void Awake()
    {
        characterController = GetComponent<JUCharacterController>();
        rb = GetComponent<Rigidbody>();
        inputManager = GetComponent<JUInputManager>();
        health = GetComponent<JUHealth>();
        inventory = GetComponent<JUInventory>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // 1. Position on spawn platform if spawned at default origin
            if (transform.position == Vector3.zero || transform.position.magnitude < 1f)
            {
                transform.position = new Vector3(31.43f, 0.5f, 30.66f);
                transform.rotation = Quaternion.identity;
            }

            // 2. Disable/destroy any offline dummy characters in the scene so this networked player is primary
            CleanupOfflineSceneCharacters();

            // 3. LOCAL PLAYER: Enable physics, JU TPS controller and input logic
            if (inputManager != null) inputManager.enabled = true;
            if (characterController != null) characterController.enabled = true;
            if (rb != null) rb.isKinematic = false;

            // 4. Bind TPS Camera Controller
            TPSCameraController tpsCamera = FindFirstObjectByType<TPSCameraController>(FindObjectsInactive.Include);
            if (tpsCamera != null && characterController != null)
            {
                tpsCamera.TargetToFollow = transform;
                tpsCamera.characterTarget = characterController;
                tpsCamera.enabled = true;
            }

            // 5. Bind Inventory UI to this player
            var invUI = FindFirstObjectByType<InventoryUIManager>(FindObjectsInactive.Include);
            if (invUI != null)
            {
                if (inventory != null) invUI.TargetInventory = inventory;
                invUI.Player = transform;
            }

            // 6. Lock cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log($"[JUNetworkPlayer] Local player successfully spawned at {transform.position} with camera and controls linked.");
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

    private void CleanupOfflineSceneCharacters()
    {
        var allCharacters = FindObjectsByType<JUCharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ch in allCharacters)
        {
            if (ch.gameObject != this.gameObject)
            {
                var netObj = ch.GetComponent<NetworkObject>();
                if (netObj == null || !netObj.IsSpawned)
                {
                    Debug.Log($"[JUNetworkPlayer] Deactivating offline placeholder character: {ch.gameObject.name}");
                    ch.gameObject.SetActive(false);
                }
            }
        }
    }
}
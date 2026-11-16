using System;
using Unity.Netcode;
using UnityEngine;
using JUTPS;
using JUTPS.CameraSystems;
using JUTPS.JUInputSystem;
using JUTPS.InventorySystem;
using JUTPS.InventorySystem.UI;
using Railbound.Gameplay;
using Project.Character;

public class JUNetworkPlayer : NetworkBehaviour
{
    private JUCharacterController characterController;
    private Rigidbody rb;
    private JUHealth health;
    private JUInventory inventory;
    private PlayerSonarSkill sonarSkill;
    private CharacterSkinRetargeter skinRetargeter;
    private Animator anim;

    private static GameObject s_deactivatedOfflinePlayer;

    private readonly NetworkVariable<int> rightHandItem = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<Railbound.BR.PuppetPose> pose = new NetworkVariable<Railbound.BR.PuppetPose>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private float nextPosePublish;
    private bool isPuppet;

    // Cached array to avoid allocations in SuppressDummyMannequinMeshes
    private static readonly string[] DummyNames = new string[] {
        "Left Foot Armor", "Left Pant Amor", "Left Upper Pant Armor",
        "Right Foot Armor", "Right Pant Amor", "Right Upper Pant Armor",
        "Helmet Armor", "Chest Armor", "Leg Armor", "Foot Armor",
        "Arms", "Body", "HeadGlass", "Torso"
    };

    private void Awake()
    {
        characterController = GetComponent<JUCharacterController>();
        rb = GetComponent<Rigidbody>();
        health = GetComponent<JUHealth>();
        inventory = GetComponent<JUInventory>();
        sonarSkill = GetComponent<PlayerSonarSkill>();
        skinRetargeter = GetComponent<CharacterSkinRetargeter>();
        anim = GetComponent<Animator>();

        SuppressDummyMannequinMeshes();
    }

    public override void OnNetworkSpawn()
    {
        SuppressDummyMannequinMeshes();

        if (!IsOwner)
        {
            rightHandItem.OnValueChanged += OnRightHandItemChanged;
            ApplyRemoteItem(rightHandItem.Value);
        }

        if (IsOwner)
        {
            JUCharacterController offlineChar = FindOfflinePlayer();
            if (Railbound.BR.BRMatchSetup.Instance != null &&
                Railbound.BR.BRMatchSetup.Instance.TryGetPlayerSpawn((int)OwnerClientId, out Vector3 randomSpawn, out Quaternion randomRotation))
            {
                transform.SetPositionAndRotation(randomSpawn, randomRotation);
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            else if (transform.position == Vector3.zero || transform.position.sqrMagnitude < 1f)
            {
                if (offlineChar != null)
                {
                    float offsetX = (OwnerClientId % 4) * 2.0f;
                    float offsetZ = (OwnerClientId / 4) * 2.0f;
                    transform.position = offlineChar.transform.position + new Vector3(offsetX, 0.2f, offsetZ);
                    transform.rotation = offlineChar.transform.rotation;
                }
                else
                {
                    transform.position = new Vector3(0f, 0.5f, 0f);
                    transform.rotation = Quaternion.identity;
                }
            }

            if (TryGetSpawnOverride(out Vector3 testSpawn, out float testYaw))
            {
                transform.position = testSpawn;
                if (!float.IsNaN(testYaw)) transform.rotation = Quaternion.Euler(0f, testYaw, 0f);
                if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            }

            CleanupOfflineSceneCharacters();

            if (characterController != null)
            {
                characterController.enabled = true;
                characterController.UseDefaultControllerInput = true;
                characterController.DisableAllMove = false;
                characterController.CanMove = true;
                characterController.CanRotate = true;
                characterController.IsArtificialIntelligence = false;
            }
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
            if (anim != null) anim.enabled = true;
            if (sonarSkill != null) sonarSkill.enabled = true;

            JUGameManager.PlayerController = characterController;
            if (JUInput.Instance() != null)
            {
                JUInput.Instance().enabled = true;
            }

            if (skinRetargeter != null)
            {
                skinRetargeter.enabled = true;
                skinRetargeter.AutoRebindAllBones();
                skinRetargeter.UpdateSkinPose();
            }

            TPSCameraController tpsCamera = FindFirstObjectByType<TPSCameraController>(FindObjectsInactive.Include);
            if (tpsCamera != null && characterController != null)
            {
                tpsCamera.characterTarget = characterController;
                tpsCamera.TargetToFollow = characterController.HumanoidSpine != null ? characterController.HumanoidSpine : transform;
                tpsCamera.enabled = true;

                characterController.MyPivotCamera = tpsCamera;
                var camField = typeof(JUCharacterController).GetField("MyCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                camField?.SetValue(characterController, tpsCamera.mCamera != null ? tpsCamera.mCamera : Camera.main);
            }

            var invUI = FindFirstObjectByType<InventoryUIManager>(FindObjectsInactive.Include);
            if (invUI != null)
            {
                if (inventory != null) invUI.TargetInventory = inventory;
                invUI.Player = transform;
            }

            if (offlineChar != null)
            {
                int rebound = Railbound.Networking.PlayerReferenceRebinder.Rebind(offlineChar.transform, transform);
                Debug.Log($"[JUNetworkPlayer] Rebound {rebound} HUD/system reference(s) from the offline player to the network player.");
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (Railbound.Gameplay.Map1DeathRestartController.Instance != null)
            {
                Railbound.Gameplay.Map1DeathRestartController.Instance.RegisterPlayer(gameObject);
            }

            Debug.Log($"[JUNetworkPlayer] Local player (ClientId: {OwnerClientId}) successfully spawned at {transform.position} yaw={transform.eulerAngles.y:F1} with camera and controls linked.");
        }
        else
        {
            if (characterController != null)
            {
                characterController.UseDefaultControllerInput = false;
                isPuppet = Railbound.BR.PuppetPoseSync.MakePuppet(characterController);
                if (!isPuppet) characterController.enabled = false;
                else ApplyRemoteItem(rightHandItem.Value);
            }
            if (rb != null) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
            if (sonarSkill != null) sonarSkill.enabled = false;

            if (skinRetargeter != null)
            {
                skinRetargeter.enabled = true;
                skinRetargeter.AutoRebindAllBones();
            }
        }
    }

    private static bool TryGetSpawnOverride(out Vector3 position, out float yaw)
    {
        position = default;
        yaw = float.NaN;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "-spawnat", StringComparison.OrdinalIgnoreCase)) continue;
            string[] parts = args[i + 1].Split(',');
            if (parts.Length != 3 && parts.Length != 4) return false;
            if (parts.Length == 4)
                float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out yaw);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (float.TryParse(parts[0], System.Globalization.NumberStyles.Float, ci, out float x) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, ci, out float y) &&
                float.TryParse(parts[2], System.Globalization.NumberStyles.Float, ci, out float z))
            {
                position = new Vector3(x, y, z);
                return true;
            }
        }
        return false;
    }

    private void SuppressDummyMannequinMeshes()
    {
        foreach (var name in DummyNames)
        {
            Transform t = FindDeepChild(transform, name);
            if (t != null)
            {
                var r = t.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                if (t.name == "Arms" || t.name == "Body" || t.name == "HeadGlass" || t.name == "Torso")
                {
                    t.gameObject.SetActive(false);
                }
            }
        }
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null) return null;
        Transform direct = parent.Find(childName);
        if (direct != null) return direct;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform res = FindDeepChild(parent.GetChild(i), childName);
            if (res != null) return res;
        }
        return null;
    }

    private void OnRightHandItemChanged(int previous, int current)
    {
        ApplyRemoteItem(current);
    }

    private void ApplyRemoteItem(int id)
    {
        if (inventory == null || inventory.HoldableItensRightHand == null) return;
        if (id >= 0)
        {
            foreach (var item in inventory.HoldableItensRightHand)
            {
                if (item != null && item.ItemSwitchID == id)
                {
                    item.Unlocked = true;
                    if (item.ItemQuantity <= 0) item.ItemQuantity = 1;
                    break;
                }
            }
        }
        Railbound.BR.PuppetPoseSync.SwitchItem(characterController, inventory, id);
    }

    private float nextGroundCheck, airborneSince = -1f;
    private void GroundGuard()
    {
        if (Time.unscaledTime < nextGroundCheck || rb == null) return;
        nextGroundCheck = Time.unscaledTime + 0.5f;
        var terrain = Terrain.activeTerrain;
        if (terrain == null || (health != null && health.IsDead)) { airborneSince = -1f; return; }

        Vector3 p = transform.position;
        float ground = terrain.SampleHeight(p) + terrain.transform.position.y;
        float above = p.y - ground;
        bool fell = above < -25f;
        bool inAir = characterController != null && !characterController.IsGrounded && !characterController.IsDriving && above > 30f;
        if (inAir) { if (airborneSince < 0f) airborneSince = Time.unscaledTime; } else airborneSince = -1f;
        bool stuckHigh = airborneSince >= 0f && Time.unscaledTime - airborneSince > 4f;
        if (!fell && !stuckHigh) return;

        Vector3 target = new Vector3(p.x, ground + 1.2f, p.z);
        Debug.LogWarning($"[JUNetworkPlayer] Ground guard: player {OwnerClientId} was {above:F0} m {(fell ? "below" : "above")} the ground at {p}; placing it at {target}.");
        rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null && nt.CanCommitToTransform) nt.Teleport(target, transform.rotation, transform.localScale);
        else transform.position = target;
        rb.position = target;
        airborneSince = -1f;
    }

    private void Update()
    {
        if (IsSpawned && IsOwner) GroundGuard();
        if (IsSpawned && IsOwner) Railbound.BR.PuppetPoseSync.Publish(characterController, pose, ref nextPosePublish);
        if (isPuppet && IsSpawned && !IsOwner) characterController.PuppetUpdate(pose.Value.Firing, pose.Value.Reloading, pose.Value.Look);

        if (IsOwner && IsSpawned && inventory != null && rightHandItem.Value != inventory.CurrentRightHandItemID)
        {
            rightHandItem.Value = inventory.CurrentRightHandItemID;
        }

        if (IsOwner)
        {
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked
                && !JUTPS.JUPauseGame.IsPaused && Time.timeScale > 0f)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        rightHandItem.OnValueChanged -= OnRightHandItemChanged;
        if (IsOwner && s_deactivatedOfflinePlayer != null)
        {
            s_deactivatedOfflinePlayer.SetActive(true);
            s_deactivatedOfflinePlayer = null;
        }
    }

    private JUCharacterController FindOfflinePlayer()
    {
        var allCharacters = FindObjectsByType<JUCharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ch in allCharacters)
        {
            if (ch.gameObject != this.gameObject)
            {
                var netObj = ch.GetComponent<NetworkObject>();
                if (netObj == null || !netObj.IsSpawned)
                {
                    if (ch.CompareTag("Player") || ch.gameObject.name.Equals("TPS Character", StringComparison.OrdinalIgnoreCase))
                    {
                        return ch;
                    }
                }
            }
        }
        return null;
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
                    if (ch.CompareTag("Player") || ch.gameObject.name.Equals("TPS Character", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"[JUNetworkPlayer] Deactivating offline placeholder character: {ch.gameObject.name}");
                        s_deactivatedOfflinePlayer = ch.gameObject;
                        ch.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Project.Character;
using Railbound.Gameplay;
using Project.UI;

namespace Railbound.Editor
{
    public static class SyncTPSCharacterPrefab
    {
        [MenuItem("Tools/Sync Netcode TPS Character Prefab")]
        public static string Sync()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.name.Contains("MAP-BR"))
            {
                EditorSceneManager.OpenScene("Assets/TerrainDemoScene_HDRP/Scenes/MAP-BR.unity");
            }

            GameObject scenePlayer = GameObject.Find("TPS Character");
            if (scenePlayer == null)
            {
                scenePlayer = GameObject.FindGameObjectWithTag("Player");
            }
            if (scenePlayer == null)
            {
                return "ERROR: Scene player 'TPS Character' not found in MAP-BR!";
            }

            string prefabPath = "Assets/SandBox/Networking/Prefabs/TPS Character.prefab";
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                return "ERROR: Failed to load prefab contents from " + prefabPath;
            }

            try
            {
                // 1. Remove NetworkRigidbody if present to avoid freezing physics on client
                var netRb = prefabRoot.GetComponent<Unity.Netcode.Components.NetworkRigidbody>();
                if (netRb != null)
                {
                    UnityEngine.Object.DestroyImmediate(netRb, true);
                    Debug.Log("[SyncTPSCharacterPrefab] Removed conflicting NetworkRigidbody component.");
                }

                // 2. Configure Rigidbody
                var rb = prefabRoot.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }

                // 3. Disable unskinned dummy mannequin meshes
                string[] dummyMeshes = new string[] { "Arms", "Body", "HeadGlass", "Torso" };
                foreach (var meshName in dummyMeshes)
                {
                    Transform t = prefabRoot.transform.Find(meshName);
                    if (t != null)
                    {
                        t.gameObject.SetActive(false);
                        Debug.Log($"[SyncTPSCharacterPrefab] Disabled dummy mesh '{meshName}'.");
                    }
                }

                // 4. Ensure Anim_Idle_MIL_R5 hierarchy is child of prefab
                Transform milSkinTransform = prefabRoot.transform.Find("Anim_Idle_MIL_R5");
                if (milSkinTransform == null)
                {
                    Transform sceneSkin = scenePlayer.transform.Find("Anim_Idle_MIL_R5");
                    GameObject newSkin = null;
                    if (sceneSkin != null)
                    {
                        newSkin = UnityEngine.Object.Instantiate(sceneSkin.gameObject);
                        newSkin.name = "Anim_Idle_MIL_R5";
                    }
                    else
                    {
                        var skinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/character/Player_1/Prefab/Anim_Idle_MIL_R5.prefab");
                        if (skinPrefab != null)
                        {
                            newSkin = UnityEngine.Object.Instantiate(skinPrefab);
                            newSkin.name = "Anim_Idle_MIL_R5";
                        }
                    }

                    if (newSkin != null)
                    {
                        newSkin.transform.SetParent(prefabRoot.transform, false);
                        newSkin.transform.localPosition = Vector3.zero;
                        newSkin.transform.localRotation = Quaternion.identity;
                        newSkin.transform.localScale = Vector3.one;
                        newSkin.SetActive(true);
                        milSkinTransform = newSkin.transform;
                        Debug.Log("[SyncTPSCharacterPrefab] Added Anim_Idle_MIL_R5 child to prefab.");
                    }
                }
                else
                {
                    milSkinTransform.gameObject.SetActive(true);
                }

                // 5. Setup CharacterSkinRetargeter on prefab
                var retargeter = prefabRoot.GetComponent<CharacterSkinRetargeter>();
                if (retargeter == null)
                {
                    retargeter = prefabRoot.AddComponent<CharacterSkinRetargeter>();
                }

                var sceneRetargeter = scenePlayer.GetComponent<CharacterSkinRetargeter>();
                if (sceneRetargeter != null)
                {
                    retargeter.HipsPositionOffset = sceneRetargeter.HipsPositionOffset;
                    retargeter.ProneHipsOffset = sceneRetargeter.ProneHipsOffset;
                    retargeter.AlignLegsToFootPlacement = sceneRetargeter.AlignLegsToFootPlacement;
                    retargeter.FootHeightOffset = sceneRetargeter.FootHeightOffset;
                    retargeter.AlignHandsToWeaponGrip = sceneRetargeter.AlignHandsToWeaponGrip;

                    retargeter.LeftUpperLegRotOffset = sceneRetargeter.LeftUpperLegRotOffset;
                    retargeter.LeftLowerLegRotOffset = sceneRetargeter.LeftLowerLegRotOffset;
                    retargeter.LeftFootRotOffset = sceneRetargeter.LeftFootRotOffset;
                    retargeter.LeftToeRotOffset = sceneRetargeter.LeftToeRotOffset;

                    retargeter.RightUpperLegRotOffset = sceneRetargeter.RightUpperLegRotOffset;
                    retargeter.RightLowerLegRotOffset = sceneRetargeter.RightLowerLegRotOffset;
                    retargeter.RightFootRotOffset = sceneRetargeter.RightFootRotOffset;
                    retargeter.RightToeRotOffset = sceneRetargeter.RightToeRotOffset;

                    retargeter.LeftUpperArmRotOffset = sceneRetargeter.LeftUpperArmRotOffset;
                    retargeter.LeftLowerArmRotOffset = sceneRetargeter.LeftLowerArmRotOffset;
                    retargeter.LeftHandRotOffset = sceneRetargeter.LeftHandRotOffset;

                    retargeter.RightUpperArmRotOffset = sceneRetargeter.RightUpperArmRotOffset;
                    retargeter.RightLowerArmRotOffset = sceneRetargeter.RightLowerArmRotOffset;
                    retargeter.RightHandRotOffset = sceneRetargeter.RightHandRotOffset;

                    retargeter.BoneMappings = new List<CharacterSkinRetargeter.BoneMapping>();
                    foreach (var sm in sceneRetargeter.BoneMappings)
                    {
                        var m = new CharacterSkinRetargeter.BoneMapping
                        {
                            Name = sm.Name,
                            RotationOffset = sm.RotationOffset,
                            IsFingerOrTwist = sm.IsFingerOrTwist
                        };
                        retargeter.BoneMappings.Add(m);
                    }
                }

                retargeter.AutoRebindAllBones();
                retargeter.enabled = true;

                // 6. Add auxiliary gameplay components if present on scene player
                if (prefabRoot.GetComponent<PlayerTargetIndicatorsController>() == null && scenePlayer.GetComponent<PlayerTargetIndicatorsController>() != null)
                {
                    prefabRoot.AddComponent<PlayerTargetIndicatorsController>();
                }
                if (prefabRoot.GetComponent<PlayerSurvivalTerrorController>() == null && scenePlayer.GetComponent<PlayerSurvivalTerrorController>() != null)
                {
                    prefabRoot.AddComponent<PlayerSurvivalTerrorController>();
                }
                if (prefabRoot.GetComponent<PlayerMedkitSystem>() == null && scenePlayer.GetComponent<PlayerMedkitSystem>() != null)
                {
                    prefabRoot.AddComponent<PlayerMedkitSystem>();
                }

                // 7. Save updated prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[SyncTPSCharacterPrefab] Saved updated Netcode TPS Character prefab successfully!");
                return "SUCCESS";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
#endif


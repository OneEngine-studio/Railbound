#if UNITY_EDITOR
using System.Collections.Generic;
using JUTPS;
using JUTPS.AI;
using Railbound.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Railbound.Editor
{
    public static class BattleRoyaleCombatantSetup
    {
        [MenuItem("Tools/Setup Battle Royale Shooting AI")]
        public static string Setup()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.name.Contains("MAP-1"))
            {
                EditorSceneManager.OpenScene("Assets/Project/MAP-1.unity");
            }

            // 1. Completely Remove all Zombie AI and Zombie Spawners
            int removedZombies = 0;
            string[] zombieNames = new string[]
            {
                "Zombie Spawner",
                "Zombi AI Sample",
                "Zombi AI Sample (1)",
                "Zombi AI Sample (2)"
            };

            foreach (var zName in zombieNames)
            {
                var go = GameObject.Find(zName);
                if (go != null)
                {
                    Undo.DestroyObjectImmediate(go);
                    removedZombies++;
                }
            }

            // Also search for any remaining objects with "zomb" in name
            var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGOs)
            {
                if (go != null && go.scene.name == "MAP-1" && go.name.ToLower().Contains("zomb"))
                {
                    Undo.DestroyObjectImmediate(go);
                    removedZombies++;
                }
            }

            // 2. Setup Low Health and Survival Terror on Player
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                var health = playerGO.GetComponent<JUHealth>();
                if (health != null)
                {
                    health.MaxHealth = 50f; // High-stakes lethality (2-3 hits kill)
                    health.Health = 50f;
                }

                var terror = playerGO.GetComponent<PlayerSurvivalTerrorController>();
                if (terror == null) terror = playerGO.AddComponent<PlayerSurvivalTerrorController>();
                terror.maxSurvivalHealth = 50f;
            }

            // 3. Load Shooting AI Prefab
            string prefabPath = "Assets/all_Projects/Demos/Demo Prefabs/AI/Patrol AI Sample.prefab";
            var shootingAIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (shootingAIPrefab == null)
            {
                return $"ERROR: Shooting AI prefab not found at {prefabPath}";
            }

            // 4. Create or Reset [BR] AI Combatants Group
            var groupGO = GameObject.Find("[BR] AI Combatants");
            if (groupGO != null)
            {
                Undo.DestroyObjectImmediate(groupGO);
            }
            groupGO = new GameObject("[BR] AI Combatants");
            Undo.RegisterCreatedObjectUndo(groupGO, "Create [BR] AI Combatants");

            // 5. Tactical Battle Royale Spawn Positions across MAP-1 (1600m)
            var spawnConfigs = new List<(string name, Vector3 pos, float yaw)>
            {
                // Central Train Depot & Loading Platforms
                ("Depot_Platform_Guard", new Vector3(12f, 0.05f, 10f), 45f),
                ("Depot_LoadingBay_Patrol", new Vector3(-15f, 0.05f, -10f), 120f),
                ("Depot_ContainerYard_Sentry", new Vector3(35f, 0.05f, -25f), -75f),
                ("Depot_Switchyard_Scout", new Vector3(-25f, 0.05f, 25f), 180f),

                // North Radar Station & High Bunker Mountain
                ("Radar_Bunker_Defender", new Vector3(14f, 32.5f, 178f), 190f),
                ("Radar_Mountain_Sniper", new Vector3(1f, 32.5f, 172f), 170f),
                ("Radar_Tunnel_Patrol", new Vector3(-8f, 30.5f, 185f), 90f),
                ("Radar_Pass_Guard", new Vector3(22f, 28.0f, 150f), 210f),

                // Northwest Quarry & Derailed Train Compound
                ("Quarry_Minecart_Sentry", new Vector3(-145f, -1.8f, 126f), 30f),
                ("Quarry_DerailedTrain_Ambush", new Vector3(-155f, -1.8f, 134f), 100f),
                ("Quarry_ToolShed_Guard", new Vector3(-150f, -1.8f, 145f), 180f),
                ("Quarry_Ridge_Lookout", new Vector3(-135f, 1.5f, 115f), -60f),

                // Shooting House & Training Compound
                ("Compound_ShootingHouse_Defender", new Vector3(38f, 0.05f, 42f), 240f),
                ("Compound_Perimeter_Patrol", new Vector3(45f, 0.05f, 15f), -90f),

                // Outer Battlefield Compounds & Vantage Points
                ("Lake_Overlook_Scout", new Vector3(180f, 6.0f, -120f), -135f),
                ("South_Valley_Road_Guard", new Vector3(-60f, 1.5f, -80f), 45f)
            };

            int spawnedCount = 0;
            foreach (var cfg in spawnConfigs)
            {
                Vector3 spawnPos = cfg.pos;
                if (Physics.Raycast(new Vector3(spawnPos.x, spawnPos.y + 10f, spawnPos.z), Vector3.down, out RaycastHit hit, 30f))
                {
                    spawnPos.y = hit.point.y + 0.05f;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(shootingAIPrefab);
                instance.name = $"BR_Combatant_{cfg.name}";
                instance.transform.SetParent(groupGO.transform, false);
                instance.transform.position = spawnPos;
                instance.transform.rotation = Quaternion.Euler(0f, cfg.yaw, 0f);
                instance.tag = "Enemy";

                // Configure tactical Battle Royale free-for-all AI settings
                var patrol = instance.GetComponent<PatrolAI>();
                if (patrol != null)
                {
                    patrol.TargetTags = new string[] { "Player", "Enemy" }; // Engages player AND other bots!
                    patrol.FieldOfView = new FieldView(75f, 150f);          // 75m sight radius, 150 deg FOV
                    patrol.AttackAtDistance = 45f;
                    patrol.StartRunningAtDistance = 25f;
                    patrol.StopDistance = 8f;
                    patrol.MinTimeToAttack = 0.2f;
                    patrol.MaxTimeToAttack = 1.0f;
                    patrol.AttackDuration = 1.4f;
                }

                var rlAgent = instance.GetComponent<Project.AI.RL.JUCharacterRLAgent>();
                if (rlAgent != null)
                {
                    rlAgent.TargetTags = new string[] { "Player", "Enemy" };
                    rlAgent.DetectionRadius = 120f;
                    rlAgent.OptimalCombatRange = 25f;
                }

                Undo.RegisterCreatedObjectUndo(instance, "Spawn Shooting Combatant");
                spawnedCount++;
            }

            // Also update any preexisting Patrol AI and RL Agents in the scene to target all enemies
            foreach (var ai in Object.FindObjectsByType<PatrolAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (ai != null)
                {
                    ai.TargetTags = new string[] { "Player", "Enemy" };
                    ai.FieldOfView = new FieldView(75f, 150f);
                    ai.AttackAtDistance = 45f;
                }
            }

            foreach (var rl in Object.FindObjectsByType<Project.AI.RL.JUCharacterRLAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (rl != null)
                {
                    rl.TargetTags = new string[] { "Player", "Enemy" };
                    rl.DetectionRadius = 120f;
                    rl.OptimalCombatRange = 25f;
                }
            }

            // Save Scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            return $"SUCCESS: Low Player Health (50 HP) & Survival Terror configured. Removed {removedZombies} zombies. Spawned {spawnedCount} Battle Royale Free-for-All Shooting AI Combatants (75m FOV)!";
        }
    }
}
#endif

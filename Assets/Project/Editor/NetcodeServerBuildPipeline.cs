using System;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Railbound.UI;

namespace Railbound.Editor
{
    /// <summary>
    /// Automated Build Pipeline and Scene Setup for Railbound Netcode Dedicated Server.
    /// Provides menu items and batchmode execution entry points for Linux & Windows Dedicated Server builds.
    /// </summary>
    public static class NetcodeServerBuildPipeline
    {
        private const string MainGameScene = "Assets/TerrainDemoScene_HDRP/Scenes/MAP-BR.unity";
        private const string LinuxBuildPath = "Builds/Server/Linux/RailboundServer.x86_64";
        private const string WindowsBuildPath = "Builds/Server/Windows/RailboundServer.exe";
        private const string WindowsClientBuildPath = "Builds/Client/Windows/Railbound.exe";
        private const string PlayerPrefabPath = "Assets/SandBox/Networking/Prefabs/TPS Character.prefab";
        private const string NetworkPrefabsAssetPath = "Assets/DefaultNetworkPrefabs.asset";

        [MenuItem("Tools/Railbound/Netcode/Setup [NETWORKING] in MAP-BR Scene", false, 10)]
        public static void SetupNetworkingInActiveScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.Equals(MainGameScene, StringComparison.OrdinalIgnoreCase))
            {
                if (EditorUtility.DisplayDialog("Open MAP-BR Scene?", 
                    $"Current active scene is '{activeScene.name}'. Open MAP-BR to setup networking?", "Yes", "No"))
                {
                    EditorSceneManager.OpenScene(MainGameScene);
                }
            }

            // 1. Locate or create [NETWORKING] root object
            GameObject netRoot = GameObject.Find("[NETWORKING]");
            if (netRoot == null)
            {
                netRoot = new GameObject("[NETWORKING]");
                Undo.RegisterCreatedObjectUndo(netRoot, "Create [NETWORKING] Root");
            }

            // 2. Setup UnityTransport
            var transport = netRoot.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = Undo.AddComponent<UnityTransport>(netRoot);
            }
            transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");

            // 3. Setup NetworkManager
            var networkManager = netRoot.GetComponent<NetworkManager>();
            if (networkManager == null)
            {
                networkManager = Undo.AddComponent<NetworkManager>(netRoot);
            }

            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.EnableSceneManagement = true;
            networkManager.NetworkConfig.TickRate = 30;

            // Assign Player Prefab
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab != null)
            {
                networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
                Debug.Log($"[NetcodeSetup] Assigned PlayerPrefab: {playerPrefab.name}");
            }
            else
            {
                Debug.LogWarning($"[NetcodeSetup] PlayerPrefab not found at {PlayerPrefabPath}");
            }

            // Assign NetworkPrefabsList
            var prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsAssetPath);
            if (prefabsList != null)
            {
                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabsList);
                Debug.Log($"[NetcodeSetup] Assigned NetworkPrefabsList: {prefabsList.name}");
            }

            // 4. Setup TacticalMultiplayerOverlay
            var overlay = netRoot.GetComponent<TacticalMultiplayerOverlay>();
            if (overlay == null)
            {
                overlay = Undo.AddComponent<TacticalMultiplayerOverlay>(netRoot);
            }

            // Mark Scene Dirty and Save
            EditorUtility.SetDirty(netRoot);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("<color=#00FF88>[NetcodeSetup] [NETWORKING] successfully configured and saved in MAP-BR!</color>");
        }

        /// <summary>
        /// Batch-mode entry point for CI (GitHub Actions / game-ci):
        /// -executeMethod Railbound.Editor.NetcodeServerBuildPipeline.BuildLinuxServerCI
        /// Exits the editor with code 0 on success and 1 on failure so the pipeline step fails correctly.
        /// </summary>
        public static void BuildLinuxServerCI()
        {
            bool ok = BuildLinuxServer();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("Tools/Railbound/Netcode/Build Linux Dedicated Server (Docker Target)", false, 20)]
        public static bool BuildLinuxServer()
        {
            return PerformServerBuild(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server, LinuxBuildPath);
        }

        [MenuItem("Tools/Railbound/Netcode/Build Windows Dedicated Server", false, 21)]
        public static bool BuildWindowsServer()
        {
            return PerformServerBuild(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Server, WindowsBuildPath);
        }

        [MenuItem("Tools/Railbound/Netcode/Build Windows Client (MAP-BR)", false, 22)]
        public static bool BuildWindowsClient()
        {
            return PerformClientBuild(BuildTarget.StandaloneWindows64, WindowsClientBuildPath);
        }

        [MenuItem("Tools/Railbound/Netcode/Open Server Builds Directory", false, 30)]
        public static void OpenBuildsFolder()
        {
            string absPath = Path.GetFullPath("Builds/Server");
            if (!Directory.Exists(absPath)) Directory.CreateDirectory(absPath);
            EditorUtility.RevealInFinder(absPath);
        }

        [MenuItem("Tools/Railbound/Netcode/Open Client Builds Directory", false, 31)]
        public static void OpenClientBuildsFolder()
        {
            string absPath = Path.GetFullPath("Builds/Client/Windows");
            if (!Directory.Exists(absPath)) Directory.CreateDirectory(absPath);
            EditorUtility.RevealInFinder(absPath);
        }

        public static bool PerformClientBuild(BuildTarget target, string outputPath)
        {
            Debug.Log($"<color=#00D0FF>[ClientBuild] Starting {target} Client build to: {outputPath}...</color>");

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string[] scenes = new[] { MainGameScene };

            var addrSettings = AddressableAssetSettingsDefaultObject.Settings;
            var prevAddrOption = addrSettings != null ? addrSettings.BuildAddressablesWithPlayerBuild : AddressableAssetSettings.PlayerBuildOption.PreferencesValue;

            try
            {
                if (addrSettings != null)
                {
                    addrSettings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                }

                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = target,
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"<color=#00FF88>[ClientBuild] Succeeded! Total Size: {summary.totalSize / (1024 * 1024):F1} MB in {summary.totalTime.TotalSeconds:F1}s</color>");
                    return true;
                }
                else
                {
                    Debug.LogError($"[ClientBuild] Failed with result: {summary.result} (Total Errors: {summary.totalErrors})");
                    return false;
                }
            }
            finally
            {
                if (addrSettings != null)
                {
                    addrSettings.BuildAddressablesWithPlayerBuild = prevAddrOption;
                }
            }
        }

        private static bool PerformServerBuild(BuildTarget target, StandaloneBuildSubtarget subtarget, string outputPath)
        {
            Debug.Log($"<color=#00D0FF>[ServerBuild] Starting {target} ({subtarget}) build to: {outputPath}...</color>");

            // Ensure destination directory exists
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Setup scenes
            string[] scenes = new[] { MainGameScene };

            // Store previous settings
            var prevTarget = EditorUserBuildSettings.activeBuildTarget;
            var prevGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var prevSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;

            // Suppress Addressables build report modal popup during automated builds
            var addrSettings = AddressableAssetSettingsDefaultObject.Settings;
            var prevAddrOption = addrSettings != null ? addrSettings.BuildAddressablesWithPlayerBuild : AddressableAssetSettings.PlayerBuildOption.PreferencesValue;

            try
            {
                if (addrSettings != null)
                {
                    addrSettings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                }

                EditorUserBuildSettings.standaloneBuildSubtarget = subtarget;

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = target,
                    subtarget = (int)subtarget,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"<color=#00FF88>[ServerBuild] SUCCEEDED! Total time: {summary.totalTime.TotalSeconds:F1}s, Size: {summary.totalSize / (1024 * 1024):F1} MB</color>");
                    return true;
                }
                else
                {
                    Debug.LogError($"[ServerBuild] FAILED with result: {summary.result}. Total errors: {summary.totalErrors}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerBuild] Exception during build: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            finally
            {
                // Restore subtarget and addressables option
                EditorUserBuildSettings.standaloneBuildSubtarget = prevSubtarget;
                if (addrSettings != null)
                {
                    addrSettings.BuildAddressablesWithPlayerBuild = prevAddrOption;
                }
            }
        }
    }
}

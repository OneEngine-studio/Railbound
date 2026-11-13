#if UNITY_EDITOR
using System.Linq;
using Michsky.UI.Reach;
using Railbound.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Railbound.EditorTools
{
    /// <summary>
    /// Menu "Railbound / Import MAP-2 Settings Menu into Lobby": copies the settings screen of MAP-2's pause menu (General, Controls, Audio,
    /// Graphics, Account, with the managers behind it) into the GameLobby scene, so the lobby's gear opens exactly the same menu, with the same
    /// options and the same look, and both read and write the same saved settings. Pause-only parts (the pause page, the exit window) stay
    /// out of the way: the lobby opens the Settings page directly and its Go Back / Esc return to the lobby (see LobbyReachSettings).
    /// Running it again replaces the earlier copy, which is how a change to MAP-2's settings menu reaches the lobby.
    /// </summary>
    public static class LobbySettingsImporter
    {
        private const string LobbyPath = "Assets/Project/GameLobby.unity";
        private const string Map2Path = "Assets/TerrainDemoScene_HDRP/Scenes/MAP-2.unity";
        private const string SourceName = "Canvas - Pause Menu";
        public const string CopyName = "Canvas - Settings (MAP-2)";

        [MenuItem("Railbound/Import MAP-2 Settings Menu into Lobby")]
        public static void Import()
        {
            var lobby = EditorSceneManager.GetActiveScene();
            if (lobby.path != LobbyPath) lobby = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);

            // MAP-2 is opened next to the lobby only to read from it; it is closed again without saving
            var map2 = SceneManager.GetSceneByPath(Map2Path);
            bool openedMap2 = false;
            if (!map2.isLoaded) { map2 = EditorSceneManager.OpenScene(Map2Path, OpenSceneMode.Additive); openedMap2 = true; }

            GameObject source = null;
            foreach (var root in map2.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == SourceName) { source = t.gameObject; break; }
            if (source == null) { Debug.LogError("[LobbySettings] '" + SourceName + "' not found in MAP-2."); if (openedMap2) EditorSceneManager.CloseScene(map2, true); return; }

            // replace an earlier copy
            foreach (var root in lobby.GetRootGameObjects())
                if (root.name == CopyName || root.name == "Post Processing Volume") Object.DestroyImmediate(root);

            var wasActive = source.activeSelf;
            var clone = Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(clone, lobby);
            clone.name = CopyName;
            clone.SetActive(false);

            // drop references that still point into MAP-2 (the managers find the lobby's camera and volume by themselves)
            int cleared = ClearForeignReferences(clone, lobby);

            // the pause page and its exit window are not used in the lobby, and the settings page must not be able to open them
            var settingsRoot = clone.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Settings" && t.parent != null && t.parent.name == "Pause Menu");
            int removedListeners = 0;
            if (settingsRoot != null)
            {
                foreach (var b in settingsRoot.GetComponentsInChildren<ButtonManager>(true))
                    if (b.name == "Go Back") removedListeners += ClearPersistent(b.onClick);
                foreach (var h in settingsRoot.GetComponentsInChildren<HotkeyEvent>(true))
                    if (h.keyID == "Escape") removedListeners += ClearPersistent(h.onHotkeyPress);
            }
            else Debug.LogWarning("[LobbySettings] Pause Menu/Settings not found in the copy.");

            // above the lobby HUD (50) and its menus (60), below the level-up popup (890)
            var canvas = clone.GetComponent<Canvas>();
            if (canvas != null) { canvas.overrideSorting = false; canvas.sortingOrder = 70; }

            if (clone.GetComponent<LobbyReachSettings>() == null) clone.AddComponent<LobbyReachSettings>();

            EditorSceneManager.MarkSceneDirty(lobby);
            EditorSceneManager.SaveScene(lobby);
            if (openedMap2) EditorSceneManager.CloseScene(map2, true);
            Debug.Log($"[LobbySettings] Copied '{SourceName}' into GameLobby as '{CopyName}' ({cleared} foreign references cleared, {removedListeners} pause-only listeners removed).");
        }

        private static int ClearPersistent(UnityEventBase evt)
        {
            int n = evt.GetPersistentEventCount();
            for (int i = n - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(evt, i);
            return n;
        }

        // any serialized reference to a scene object that is not part of the lobby scene would break when the scene is saved
        private static int ClearForeignReferences(GameObject clone, Scene lobby)
        {
            int cleared = 0;
            foreach (var comp in clone.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                bool changed = false;
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    var obj = it.objectReferenceValue;
                    Scene s = default;
                    if (obj is GameObject g) s = g.scene; else if (obj is Component c) s = c.gameObject.scene; else continue;
                    if (s.IsValid() && s != lobby)
                    {
                        Debug.Log($"[LobbySettings] Cleared {comp.GetType().Name}.{it.propertyPath} -> {obj.name} ({s.name})");
                        it.objectReferenceValue = null; changed = true; cleared++;
                    }
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
            return cleared;
        }
    }
}
#endif

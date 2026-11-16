using UnityEngine;
using UnityEngine.SceneManagement;

namespace Railbound.Gameplay
{
    /// <summary>
    /// MAP-2 has a leftover soldier model saved at its scene root ("Anim_Idle_MIL_R5(Clone)", the MIL-R5 skin with its own Animator):
    /// a full character with no controller, standing at the world origin about 11 m above the terrain there. Players saw it as a
    /// soldier floating in the sky. The map is not in git, so this switches such strays off when a scene loads: a skin model only
    /// belongs under a character (CharacterSkinApplier puts it there), never at a scene root.
    /// </summary>
    public static class StraySkinModelGuard
    {
        public static int LastDisabled { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Apply(scene); }

        public static int Apply(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return 0;
            int disabled = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.activeSelf || !root.name.StartsWith(Project.Character.CharacterSkinApplier.SkinChildName)) continue;
                if (root.GetComponent<JUTPS.JUCharacterController>() != null) continue;   // a real character, whatever its name
                if (root.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) continue;
                root.SetActive(false);
                disabled++;
                Debug.Log($"[StraySkinModelGuard] Switched off the stray skin model '{root.name}' at {root.transform.position} in '{scene.name}'.");
            }
            LastDisabled = disabled;
            return disabled;
        }
    }
}

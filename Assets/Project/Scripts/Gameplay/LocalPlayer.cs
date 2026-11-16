using JUTPS;
using Unity.Netcode;
using UnityEngine;

namespace Railbound.Gameplay
{
    /// <summary>
    /// The character the person at THIS machine controls. HUD scripts used GameObject.FindGameObjectWithTag("Player"), but the network
    /// player prefab is tagged "Player", so in a match with other humans that lookup returned any of them (or the offline placeholder that
    /// is switched off when the network player spawns): the minimap arrow, the health bar or the objective card then followed another
    /// player. Use this instead.
    /// </summary>
    public static class LocalPlayer
    {
        public static JUCharacterController Controller
        {
            get
            {
                var pc = JUGameManager.PlayerController;   // JUNetworkPlayer sets it to the local network player when that spawns
                if (pc != null && pc.gameObject.activeInHierarchy && IsLocal(pc)) return pc;
                foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
                {
                    if (!go.activeInHierarchy) continue;
                    var c = go.GetComponent<JUCharacterController>();
                    if (c != null && IsLocal(c)) return c;
                }
                return null;
            }
        }

        public static Transform Transform { get { var c = Controller; return c != null ? c.transform : null; } }
        public static GameObject GameObject { get { var c = Controller; return c != null ? c.gameObject : null; } }

        public static bool IsLocal(JUCharacterController c)
        {
            if (c == null || c.IsRemotePuppet) return false;
            var np = c.GetComponent<JUNetworkPlayer>();
            if (np != null) return np.IsSpawned && np.IsOwner;
            if (c.GetComponent<Railbound.BR.BRNetBot>() != null) return false;
            // a plain scene character: the single-player player, or the offline placeholder before the network player spawns
            var nm = NetworkManager.Singleton;
            return nm == null || !nm.IsListening || !nm.IsClient || nm.LocalClient == null || nm.LocalClient.PlayerObject == null;
        }
    }
}
